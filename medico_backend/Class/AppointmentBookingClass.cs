using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class AppointmentBookingClass
    {
        private readonly string _db_conn;
        private readonly string _customer_conn;

        public AppointmentBookingClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _customer_conn = configuration.GetConnectionString("cust_conn")!;
        }

        // ─────────────────────────────────────────
        // GET AVAILABLE SLOTS
        // ─────────────────────────────────────────
        public async Task<List<DoctorAppointmentSlotDetailsModel>> GetAvailableSlots(
            int dcode, DateOnly appointment_date, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT slot_detail_id,
                                  slot_master_id,
                                  dcode,
                                  tenant_code,
                                  appointment_date,
                                  slot_start_time,
                                  slot_end_time,
                                  max_patients,
                                  max_walkin,
                                  max_online,
                                  booked_count,
                                  walkin_count,
                                  online_count,
                                  (max_patients - booked_count) AS remaining_seats,
                                  slot_status,
                                  is_active,
                                  isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   doctor_appointment_slot_details
                           WHERE  isdeleted       = false
                           AND    is_active        = true
                           AND    slot_status      = 'OPEN'
                           AND    dcode            = @dcode
                           AND    appointment_date = @appointment_date
                           AND    tenant_code      = @tenant_code
                           ORDER  BY slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(sql, new
            {
                dcode,
                appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue),
                tenant_code
            });

            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET NEXT TOKEN
        // ─────────────────────────────────────────
        public async Task<int> GetNextToken(Guid slot_detail_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT COALESCE(MAX(token_no), 0) + 1
                           FROM   appointment_booking
                           WHERE  slot_detail_id = @slot_detail_id
                           AND    tenant_code    = @tenant_code
                           AND    isdeleted      = false
                           AND    booking_status != 'CANCELLED'";

            return await db.ExecuteScalarAsync<int>(sql, new { slot_detail_id, tenant_code });
        }

        // ─────────────────────────────────────────
        // BOOK APPOINTMENT
        // ─────────────────────────────────────────
        public async Task<string> BookAppointment(AppointmentBookingModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Validate booking_type
                var allowedTypes = new[] { "WALKIN", "ONLINE", "WHATSAPP" };
                if (!allowedTypes.Contains(data.booking_type.ToUpper()))
                    return "Invalid booking_type. Allowed: WALKIN, ONLINE,WHATSAPP";

                data.booking_type = data.booking_type.ToUpper();

                // Fetch slot and check limits based on booking_type
                string checkSql = @"SELECT slot_status, booked_count, max_patients,
                                   walkin_count, max_walkin,
                                   online_count, max_online
                            FROM   doctor_appointment_slot_details  
                            WHERE  slot_detail_id = @slot_detail_id
                            AND    tenant_code    = @tenant_code
                            AND    isdeleted      = false
                            AND    is_active      = true";

                var slot = await db.QueryFirstOrDefaultAsync(
                    checkSql, new { data.slot_detail_id, data.tenant_code });

                if (slot == null) return "Slot not found";
                if (slot.slot_status != "OPEN") return "Slot is FULL or not available";
                if (slot.booked_count >= slot.max_patients) return "Slot is fully booked";

                if (data.booking_type == "WALKIN" && slot.walkin_count >= slot.max_walkin)
                    return "Walk-in limit reached for this slot";

                if ((data.booking_type == "ONLINE" || data.booking_type == "WHATSAPP")
                 && slot.online_count >= slot.max_online)
                    return "Online booking limit reached for this slot";

                data.booking_id = Guid.NewGuid();
                data.token_no = await GetNextToken(data.slot_detail_id, data.tenant_code!);
                data.booking_no = await GenerateBookingNo(db, data.tenant_code!);
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                // Only set BOOKED if not already RESCHEDULED
                if (data.booking_status != "RESCHEDULED")
                    data.booking_status = "BOOKED";

                string insertSql = @"INSERT INTO appointment_booking
(booking_id, custid, dcode, slot_detail_id, slot_master_id,
 appointment_date, slot_start_time, slot_end_time,
 token_no, booking_no, booking_status, booking_type, notes,    
 rescheduled_from, reschedule_reason,
 tenant_code, isdeleted, usercode, created_at, updated_at)
VALUES
(@booking_id, @custid, @dcode, @slot_detail_id, @slot_master_id,
 @appointment_date, @slot_start_time, @slot_end_time,
 @token_no, @booking_no, @booking_status, @booking_type, @notes,
 @rescheduled_from, @reschedule_reason,
 @tenant_code, @isdeleted, @usercode, @created_at, @updated_at)";

                await db.ExecuteAsync(insertSql, new
                {
                    data.booking_id,
                    data.custid,
                    data.dcode,
                    data.slot_detail_id,
                    data.slot_master_id,
                    appointment_date = data.appointment_date.ToDateTime(TimeOnly.MinValue),
                    slot_start_time = data.slot_start_time.ToTimeSpan(),
                    slot_end_time = data.slot_end_time.ToTimeSpan(),
                    data.token_no,
                    data.booking_no,
                    data.booking_status,
                    data.booking_type,
                    data.notes,
                    data.rescheduled_from,
                    data.reschedule_reason,
                    data.tenant_code,
                    data.isdeleted,
                    data.usercode,
                    data.created_at,
                    data.updated_at
                });

                // Increment correct counter based on booking_type
                string updateSlotSql = data.booking_type == "WALKIN"
                    ? @"UPDATE doctor_appointment_slot_details
                SET booked_count = booked_count + 1,
                    walkin_count = walkin_count + 1,
                    slot_status  = CASE
                                     WHEN booked_count + 1 >= max_patients THEN 'FULL'
                                     ELSE 'OPEN'
                                   END,
                    updated_at   = now()
                WHERE slot_detail_id = @slot_detail_id
                AND   tenant_code    = @tenant_code"

                    : @"UPDATE doctor_appointment_slot_details
                SET booked_count = booked_count + 1,
                    online_count = online_count + 1,
                    slot_status  = CASE
                                     WHEN booked_count + 1 >= max_patients THEN 'FULL'
                                     ELSE 'OPEN'
                                   END,
                    updated_at   = now()
                WHERE slot_detail_id = @slot_detail_id
                AND   tenant_code    = @tenant_code";

                await db.ExecuteAsync(updateSlotSql,
                    new { data.slot_detail_id, data.tenant_code });

                // ✅ WALKIN → store usercode | ONLINE → store custid
                string actionBy = data.booking_type == "WALKIN"
                    ? data.usercode.ToString()
                    : data.custid.ToString();

                await WriteBookingLog(db, new AppointmentBookingLogModel
                {
                    booking_id = data.booking_id,
                    booking_no = data.booking_no,
                    custid = data.custid,
                    dcode = data.dcode,
                    action = "BOOKED",
                    action_by = actionBy,   // ✅ WALKIN = usercode, ONLINE = custid
                    new_slot_detail_id = data.slot_detail_id,
                    new_appointment_date = data.appointment_date,
                    new_slot_start_time = data.slot_start_time,
                    remarks = data.notes,
                    tenant_code = data.tenant_code!
                });

                return $"Success|Token:{data.token_no}|BookingNo:{data.booking_no}|BookingId:{data.booking_id}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // CANCEL APPOINTMENT
        // ─────────────────────────────────────────
        public async Task<string> CancelAppointment(
            Guid booking_id, string cancel_reason, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                string getSql = @"SELECT * FROM appointment_booking
                                  WHERE  booking_id  = @booking_id
                                  AND    tenant_code = @tenant_code
                                  AND    isdeleted   = false";

                var booking = await db.QueryFirstOrDefaultAsync<AppointmentBookingModel>(
                    getSql, new { booking_id, tenant_code });

                if (booking == null) return "Booking not found";
                if (booking.booking_status == "CANCELLED") return "Already cancelled";
                if (booking.booking_status == "VISITED") return "Cannot cancel visited appointment";

                // Mark booking as cancelled (hard cancel — visible for audit)
                string cancelSql = @"UPDATE appointment_booking
                                     SET booking_status = 'CANCELLED',
                                         cancel_reason  = @cancel_reason,
                                         cancelled_at   = now(),
                                         updated_at     = now()
                                     WHERE booking_id  = @booking_id
                                     AND   tenant_code = @tenant_code";

                await db.ExecuteAsync(cancelSql, new { booking_id, cancel_reason, tenant_code });

                // Decrement correct counter and reopen slot if it was FULL
                string freeSlotSql = booking.booking_type == "WALKIN"
                    ? @"UPDATE doctor_appointment_slot_details
                        SET booked_count = GREATEST(booked_count - 1, 0),
                            walkin_count = GREATEST(walkin_count - 1, 0),
                            slot_status  = CASE
                                             WHEN slot_status = 'FULL' THEN 'OPEN'
                                             ELSE slot_status
                                           END,
                            updated_at   = now()
                        WHERE slot_detail_id = @slot_detail_id
                        AND   tenant_code    = @tenant_code"

                    : @"UPDATE doctor_appointment_slot_details
                        SET booked_count = GREATEST(booked_count - 1, 0),
                            online_count = GREATEST(online_count - 1, 0),
                            slot_status  = CASE
                                             WHEN slot_status = 'FULL' THEN 'OPEN'
                                             ELSE slot_status
                                           END,
                            updated_at   = now()
                        WHERE slot_detail_id = @slot_detail_id
                        AND   tenant_code    = @tenant_code";

                await db.ExecuteAsync(freeSlotSql,
                    new { booking.slot_detail_id, tenant_code });

                return "Cancelled Successfully";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // SOFT DELETE OLD BOOKING (reschedule only)
        // Old booking is hidden via isdeleted = true instead of
        // being marked CANCELLED, keeping the list clean.
        // Slot counters are decremented exactly like a cancel.
        // ─────────────────────────────────────────
        private async Task<string> SoftDeleteForReschedule(
            Guid booking_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Fetch old booking
                string getSql = @"SELECT * FROM appointment_booking
                                  WHERE  booking_id  = @booking_id
                                  AND    tenant_code = @tenant_code
                                  AND    isdeleted   = false";

                var booking = await db.QueryFirstOrDefaultAsync<AppointmentBookingModel>(
                    getSql, new { booking_id, tenant_code });

                if (booking == null) return "Booking not found";
                if (booking.booking_status == "VISITED")
                    return "Cannot reschedule a visited appointment";

                // Soft delete — set isdeleted = true, no CANCELLED status needed
                // Mark old booking as CANCELLED and soft delete — hidden from lists, status correct for audit
                string softDeleteSql = @"UPDATE appointment_booking
                         SET isdeleted      = true,
                             booking_status = 'CANCELLED',
                             cancel_reason  = 'Rescheduled',
                             cancelled_at   = now(),
                             updated_at     = now()
                         WHERE booking_id  = @booking_id
                         AND   tenant_code = @tenant_code";

                await db.ExecuteAsync(softDeleteSql, new { booking_id, tenant_code });

                // Decrement slot counters and reopen slot if it was FULL
                string freeSlotSql = booking.booking_type == "WALKIN"
                    ? @"UPDATE doctor_appointment_slot_details
                        SET booked_count = GREATEST(booked_count - 1, 0),
                            walkin_count = GREATEST(walkin_count - 1, 0),
                            slot_status  = CASE
                                             WHEN slot_status = 'FULL' THEN 'OPEN'
                                             ELSE slot_status
                                           END,
                            updated_at   = now()
                        WHERE slot_detail_id = @slot_detail_id
                        AND   tenant_code    = @tenant_code"

                    : @"UPDATE doctor_appointment_slot_details
                        SET booked_count = GREATEST(booked_count - 1, 0),
                            online_count = GREATEST(online_count - 1, 0),
                            slot_status  = CASE
                                             WHEN slot_status = 'FULL' THEN 'OPEN'
                                             ELSE slot_status
                                           END,
                            updated_at   = now()
                        WHERE slot_detail_id = @slot_detail_id
                        AND   tenant_code    = @tenant_code";

                await db.ExecuteAsync(freeSlotSql,
                    new { booking.slot_detail_id, tenant_code });

                return "Cancelled Successfully"; // keeps caller checks intact
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // RESCHEDULE APPOINTMENT
        // ─────────────────────────────────────────
        public async Task<string> RescheduleAppointment(
            RescheduleAppointmentRequest request, string tenant_code)
        {
            try
            {
                // Soft-delete old booking — keeps list clean, decrements slot counters
                var cancelResult = await SoftDeleteForReschedule(
                    request.old_booking_id, tenant_code);

                if (cancelResult != "Cancelled Successfully")
                    return $"Cancel failed: {cancelResult}";

                // Carry forward booking_type and reschedule metadata to new booking
                request.new_booking.booking_type = request.booking_type;
                request.new_booking.rescheduled_from = request.old_booking_id;
                request.new_booking.reschedule_reason = request.reschedule_reason;
                request.new_booking.booking_status = "RESCHEDULED";
                request.new_booking.tenant_code = tenant_code;

                return await BookAppointment(request.new_booking);
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // UPDATE STATUS
        // ─────────────────────────────────────────
        public async Task<string> UpdateStatus(
            Guid booking_id, string booking_status, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var allowed = new[] { "BOOKED", "CONFIRMED", "VISITED", "CANCELLED", "RESCHEDULE_PENDING", "NOT VISITED" };
                if (!allowed.Contains(booking_status.ToUpper()))
                    return $"Invalid status. Allowed: {string.Join(", ", allowed)}";

                string sql = @"UPDATE appointment_booking
                               SET booking_status = @booking_status,
                                   updated_at     = now()
                               WHERE booking_id  = @booking_id
                               AND   tenant_code = @tenant_code
                               AND   isdeleted   = false";

                int rows = await db.ExecuteAsync(sql,
                    new { booking_id, booking_status = booking_status.ToUpper(), tenant_code });

                return rows > 0 ? "Success" : "Booking not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // GET ALL BOOKINGS
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingModel>> GetAll(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT booking_id,booking_no, custid, dcode,
                                  slot_detail_id, slot_master_id,
                                  appointment_date, slot_start_time, slot_end_time,
                                  token_no, booking_status, booking_type, notes,
                                  cancel_reason, cancelled_at,
                                  rescheduled_from, reschedule_reason,
                                  tenant_code, isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   appointment_booking
                           WHERE  isdeleted   = false
                           AND    tenant_code = @tenant_code
                           ORDER  BY appointment_date DESC, token_no";

            var res = await db.QueryAsync<AppointmentBookingModel>(sql, new { tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY DATE
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingModel>> GetByDate(
            DateOnly appointment_date, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT booking_id, custid, dcode,
                                  slot_detail_id, slot_master_id,
                                  appointment_date, slot_start_time, slot_end_time,
                                  token_no, booking_status, booking_type, notes,
                                  cancel_reason, cancelled_at,
                                  rescheduled_from, reschedule_reason,
                                  tenant_code, isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   appointment_booking
                           WHERE  isdeleted        = false
                           AND    appointment_date = @appointment_date
                           AND    tenant_code      = @tenant_code
                           ORDER  BY slot_start_time, token_no";

            var res = await db.QueryAsync<AppointmentBookingModel>(sql, new
            {
                appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue),
                tenant_code
            });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET TODAY'S APPOINTMENTS BY DOCTOR
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingModel>> GetTodayAppointments(
            int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT booking_id, custid, dcode,
                                  slot_detail_id, slot_master_id,
                                  appointment_date, slot_start_time, slot_end_time,
                                  token_no, booking_status, booking_type, notes,
                                  cancel_reason, cancelled_at,
                                  rescheduled_from, reschedule_reason,
                                  tenant_code, isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   appointment_booking
                           WHERE  isdeleted        = false
                           AND    dcode            = @dcode
                           AND    appointment_date = (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata')::date
                           AND    tenant_code      = @tenant_code
                           AND    booking_status  != 'CANCELLED'
                           ORDER  BY slot_start_time, token_no";

            var res = await db.QueryAsync<AppointmentBookingModel>(sql, new { dcode, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CUSTOMER
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingModel>> GetByCustomer(
            decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT booking_id, custid, dcode,
                                  slot_detail_id, slot_master_id,
                                  appointment_date, slot_start_time, slot_end_time,
                                  token_no, booking_status, booking_type, notes,
                                  cancel_reason, cancelled_at,
                                  rescheduled_from, reschedule_reason,
                                  tenant_code, isdeleted,
                                  created_at AT TIME ZONE 'UTC' AS created_at,
                                  updated_at AT TIME ZONE 'UTC' AS updated_at
                           FROM   appointment_booking
                           WHERE  isdeleted   = false
                           AND    custid      = @custid
                           AND    tenant_code = @tenant_code
                           ORDER  BY appointment_date DESC, token_no";

            var res = await db.QueryAsync<AppointmentBookingModel>(sql, new { custid, tenant_code });
            return res.ToList();
        }

        // ─────────────────────────────────────────
        // GET CUSTOMER INFO FROM CUSTOMER DB
        // ─────────────────────────────────────────
        public async Task<CustomerMasterModel?> GetCustomerInfo(
            decimal custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_customer_conn);

            string sql = @"SELECT * FROM customer_master
                           WHERE custid      = @custid
                           AND   tenant_code = @tenant_code
                           AND   deleted     = false";

            return await db.QueryFirstOrDefaultAsync<CustomerMasterModel>(
                sql, new { custid, tenant_code });
        }

        // ─────────────────────────────────────────
        // RESCHEDULE SLOT ITEM
        // (internal — used by RescheduleWholeSlot)
        // ─────────────────────────────────────────
        private async Task<string> RescheduleSlotItem(
            RescheduleSlotItemRequest request, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Verify old booking exists and is in RESCHEDULE_PENDING state
                string checkSql = @"
                    SELECT booking_id, booking_status, custid, dcode
                    FROM   appointment_booking
                    WHERE  booking_id  = @booking_id
                    AND    tenant_code = @tenant_code
                    AND    isdeleted   = false";

                var oldBooking = await db.QueryFirstOrDefaultAsync(
                    checkSql, new
                    {
                        booking_id = request.old_booking_id,
                        tenant_code
                    });

                if (oldBooking == null)
                    return "Original booking not found";

                if (oldBooking.booking_status != "RESCHEDULE_PENDING")
                    return $"Status must be RESCHEDULE_PENDING. " +
                           $"Current: {oldBooking.booking_status}";

                // Soft-delete old booking — hides it without marking CANCELLED
                var cancelResult = await SoftDeleteForReschedule(
                    request.old_booking_id, tenant_code);

                if (cancelResult != "Cancelled Successfully")
                    return $"Cancel failed: {cancelResult}";

                // Build and book the new appointment
                var newBooking = new AppointmentBookingModel
                {
                    custid = oldBooking.custid,
                    dcode = request.new_dcode > 0
                                           ? request.new_dcode
                                           : (int)oldBooking.dcode,
                    slot_detail_id = request.new_slot_detail_id,
                    slot_master_id = request.new_slot_master_id,
                    appointment_date = request.new_appointment_date,
                    slot_start_time = request.new_slot_start_time,
                    slot_end_time = request.new_slot_end_time,
                    booking_type = request.booking_type.ToUpper(),
                    booking_status = "RESCHEDULED",
                    rescheduled_from = request.old_booking_id,
                    reschedule_reason = request.reschedule_reason,
                    notes = request.notes,
                    tenant_code = tenant_code
                };

                return await BookAppointment(newBooking);
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // RESCHEDULE WHOLE SLOT
        // Moves all active patients from an old slot into a new slot.
        // ─────────────────────────────────────────
        public async Task<object> RescheduleWholeSlot(
            RescheduleWholeSlotRequest request, string tenant_code)
        {
            var rescheduled = new List<string>();
            var failed = new List<string>();

            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // STEP 1 — Validate new slot exists and is OPEN
                string slotCheckSql = @"
                    SELECT slot_status,
                           booked_count, max_patients,
                           walkin_count, max_walkin,
                           online_count, max_online
                    FROM   doctor_appointment_slot_details
                    WHERE  slot_detail_id = @slot_detail_id
                    AND    tenant_code    = @tenant_code
                    AND    isdeleted      = false
                    AND    is_active      = true";

                var newSlot = await db.QueryFirstOrDefaultAsync(
                    slotCheckSql, new
                    {
                        slot_detail_id = request.new_slot_detail_id,
                        tenant_code
                    });

                if (newSlot == null)
                    return new { rescheduled, failed = new List<string> { "New slot not found or not available" } };

                if (newSlot.slot_status != "OPEN")
                    return new { rescheduled, failed = new List<string> { "New slot is not OPEN" } };

                // STEP 1.5 — Mark all active bookings in the old slot as RESCHEDULE_PENDING
                string markPendingSql = @"
                    UPDATE appointment_booking ab
                    SET    booking_status = 'RESCHEDULE_PENDING',
                           updated_at     = now()
                    FROM   doctor_appointment_slot_details sd
                    WHERE  sd.slot_detail_id = ab.slot_detail_id
                    AND    sd.slot_master_id = @slot_master_id
                    AND    ab.tenant_code    = @tenant_code
                    AND    ab.isdeleted      = false
                    AND    ab.booking_status NOT IN ('CANCELLED', 'VISITED', 'RESCHEDULE_PENDING')";

                await db.ExecuteAsync(markPendingSql, new
                {
                    request.slot_master_id,
                    tenant_code
                });

                // STEP 2 — Fetch all RESCHEDULE_PENDING bookings for the old slot
                string getPendingSql = @"
                    SELECT ab.booking_id,
                           ab.custid,
                           ab.dcode,
                           ab.booking_type,
                           ab.slot_detail_id,
                           ab.notes
                    FROM   appointment_booking             ab
                    JOIN   doctor_appointment_slot_details sd
                           ON sd.slot_detail_id = ab.slot_detail_id
                    WHERE  sd.slot_master_id = @slot_master_id
                    AND    ab.tenant_code    = @tenant_code
                    AND    ab.booking_status = 'RESCHEDULE_PENDING'
                    AND    ab.isdeleted      = false
                    ORDER  BY ab.token_no";

                var pendingBookings = (await db.QueryAsync(
                    getPendingSql, new { request.slot_master_id, tenant_code })).ToList();

                if (pendingBookings.Count == 0)
                    return new { rescheduled, failed = new List<string> { "No active bookings found for this slot" } };

                // STEP 3 — Check new slot has enough capacity
                int remainingSeats = (int)newSlot.max_patients - (int)newSlot.booked_count;

                if (pendingBookings.Count > remainingSeats)
                    return new
                    {
                        rescheduled,
                        failed = new List<string>
                        {
                            $"New slot only has {remainingSeats} seats " +
                            $"but {pendingBookings.Count} patients need rescheduling. " +
                            $"Please pick a bigger slot."
                        }
                    };

                // STEP 4 — Reschedule each patient into the new slot
                foreach (var booking in pendingBookings)
                {
                    try
                    {
                        var itemRequest = new RescheduleSlotItemRequest
                        {
                            old_booking_id = booking.booking_id,
                            booking_type = booking.booking_type,
                            reschedule_reason = request.reschedule_reason,
                            new_slot_detail_id = request.new_slot_detail_id,
                            new_slot_master_id = request.new_slot_master_id,
                            new_appointment_date = request.new_appointment_date,
                            new_slot_start_time = request.new_slot_start_time,
                            new_slot_end_time = request.new_slot_end_time,
                            new_dcode = request.new_dcode > 0
                                                      ? request.new_dcode
                                                      : (int)booking.dcode,
                            notes = booking.notes
                        };

                        var result = await RescheduleSlotItem(itemRequest, tenant_code);

                        if (result.StartsWith("Success"))
                            rescheduled.Add(
                                $"CustId: {booking.custid} " +
                                $"| OldBooking: {booking.booking_id} → {result}");
                        else
                            failed.Add(
                                $"CustId: {booking.custid} " +
                                $"| OldBooking: {booking.booking_id} → {result}");
                    }
                    catch (Exception ex)
                    {
                        failed.Add(
                            $"CustId: {booking.custid} " +
                            $"| OldBooking: {booking.booking_id} → {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                failed.Add($"System error: {ex.Message}");
            }

            return new { rescheduled, failed };
        }

        // ─────────────────────────────────────────
        // GET RESCHEDULE PENDING
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingModel>> GetReschedulePending(
            string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
                SELECT booking_id, custid, dcode,
                       slot_detail_id, slot_master_id,
                       appointment_date, slot_start_time, slot_end_time,
                       token_no, booking_status, booking_type,
                       cancel_reason, cancelled_at,
                       rescheduled_from, reschedule_reason,
                       tenant_code, isdeleted,
                       created_at AT TIME ZONE 'UTC' AS created_at,
                       updated_at AT TIME ZONE 'UTC' AS updated_at
                FROM   appointment_booking
                WHERE  booking_status = 'RESCHEDULE_PENDING'
                AND    tenant_code    = @tenant_code
                AND    isdeleted      = false
                ORDER  BY appointment_date, token_no";

            var res = await db.QueryAsync<AppointmentBookingModel>(
                sql, new { tenant_code });

            return res.ToList();
        }
        // ─────────────────────────────────────────
        // GENERATE BOOKING NUMBER
        // Format: YYYY/MM/0001 — resets each month
        // ─────────────────────────────────────────
        private async Task<string> GenerateBookingNo(IDbConnection db, string tenant_code)
        {
            string sql = @"SELECT COALESCE(MAX(
                       CAST(SPLIT_PART(booking_no, '/', 3) AS INT)
                   ), 0) + 1
                   FROM appointment_booking
                   WHERE tenant_code = @tenant_code
                   AND   isdeleted   = false
                   AND   SPLIT_PART(booking_no, '/', 1) = @year
                   AND   SPLIT_PART(booking_no, '/', 2) = @month";

            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month });

            return $"{year}/{month}/{next:D4}";
        }
        // ─────────────────────────────────────────
        // WRITE BOOKING LOG (patient activities only)
        // ─────────────────────────────────────────
        private async Task WriteBookingLog(IDbConnection db, AppointmentBookingLogModel log)
        {
            string sql = @"INSERT INTO appointment_booking_log
                   (log_id, booking_id, booking_no, custid, dcode,
                    action, action_by,
                    old_slot_detail_id, new_slot_detail_id,
                    old_appointment_date, new_appointment_date,
                    old_slot_start_time, new_slot_start_time,
                    remarks, tenant_code, created_at)
                   VALUES
                   (@log_id, @booking_id, @booking_no, @custid, @dcode,
                    @action, @action_by,
                    @old_slot_detail_id, @new_slot_detail_id,
                    @old_appointment_date, @new_appointment_date,
                    @old_slot_start_time, @new_slot_start_time,
                    @remarks, @tenant_code, @created_at)";

            await db.ExecuteAsync(sql, new
            {
                log.log_id,
                log.booking_id,
                log.booking_no,
                log.custid,
                log.dcode,
                log.action,
                log.action_by,
                log.old_slot_detail_id,
                log.new_slot_detail_id,
                old_appointment_date = log.old_appointment_date?.ToDateTime(TimeOnly.MinValue),
                new_appointment_date = log.new_appointment_date?.ToDateTime(TimeOnly.MinValue),
                old_slot_start_time = log.old_slot_start_time?.ToTimeSpan(),
                new_slot_start_time = log.new_slot_start_time?.ToTimeSpan(),
                log.remarks,
                log.tenant_code,
                log.created_at
            });
        }

        // ─────────────────────────────────────────
        // PATIENT RESCHEDULE
        // Same as RescheduleAppointment but with log
        // ─────────────────────────────────────────
        public async Task<string> PatientReschedule(
    PatientRescheduleRequest request, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // Fetch old booking details for log before soft deleting
                string getOldSql = @"
            SELECT booking_id, booking_no, custid, dcode,
                   slot_detail_id, appointment_date, slot_start_time,
                   booking_status
            FROM   appointment_booking
            WHERE  booking_id  = @booking_id
            AND    tenant_code = @tenant_code
            AND    isdeleted   = false";

                var oldBooking = await db.QueryFirstOrDefaultAsync<AppointmentBookingModel>(
                    getOldSql, new { booking_id = request.old_booking_id, tenant_code });

                if (oldBooking == null) return "Booking not found";
                if (oldBooking.booking_status == "CANCELLED") return "Booking already cancelled";
                if (oldBooking.booking_status == "VISITED") return "Cannot reschedule a visited appointment";

                // Soft-delete old booking — keeps list clean, decrements slot counters
                var cancelResult = await SoftDeleteForReschedule(
                    request.old_booking_id, tenant_code);

                if (cancelResult != "Cancelled Successfully")
                    return $"Cancel failed: {cancelResult}";

                // Carry forward metadata to new booking
                request.new_booking.booking_type = request.booking_type;
                request.new_booking.rescheduled_from = request.old_booking_id;
                request.new_booking.reschedule_reason = request.reschedule_reason;
                request.new_booking.booking_status = "RESCHEDULED";
                request.new_booking.tenant_code = tenant_code;

                var bookResult = await BookAppointment(request.new_booking);

                if (!bookResult.StartsWith("Success"))
                    return bookResult;

                // Parse new booking_id and booking_no from result string
                // Format: "Success|Token:1|BookingNo:2026/05/0001|BookingId:uuid"
                var parts = bookResult.Split('|');
                var newBookingId = Guid.Parse(parts[3].Replace("BookingId:", ""));
                var newBookingNo = parts[2].Replace("BookingNo:", "");

                // Write patient reschedule log — no casting needed, model types are correct
                await WriteBookingLog(db, new AppointmentBookingLogModel
                {
                    booking_id = newBookingId,
                    booking_no = newBookingNo,
                    custid = oldBooking.custid,
                    dcode = oldBooking.dcode,
                    action = "RESCHEDULED",
                    action_by = oldBooking.custid.ToString(),
                    old_slot_detail_id = oldBooking.slot_detail_id,
                    new_slot_detail_id = request.new_booking.slot_detail_id,
                    old_appointment_date = oldBooking.appointment_date,
                    new_appointment_date = request.new_booking.appointment_date,
                    old_slot_start_time = oldBooking.slot_start_time,
                    new_slot_start_time = request.new_booking.slot_start_time,
                    remarks = request.reschedule_reason,
                    tenant_code = tenant_code
                });

                return bookResult;
            }
            catch (Exception ex) { return ex.Message; }
        }
        // ─────────────────────────────────────────
        // GET APPOINTMENT LOG
        // Supports 3 query modes:
        //   1. by booking_id  → full history of one booking
        //   2. by custid      → all activity for a patient (for chat app)
        //   3. by dcode + date → doctor's day log
        // ─────────────────────────────────────────
        public async Task<List<AppointmentBookingLogViewModel>> GetAppointmentLog(
            string tenant_code,
            Guid? booking_id = null,
            decimal? custid = null,
            int? dcode = null,
            DateOnly? from_date = null,
            DateOnly? to_date = null,
            string? action_filter = null)   // BOOKED / RESCHEDULED / CANCELLED / ALL
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var where = new List<string> { "l.tenant_code = @tenant_code" };
            var param = new DynamicParameters();
            param.Add("tenant_code", tenant_code);

            if (booking_id.HasValue)
            {
                where.Add("l.booking_id = @booking_id");
                param.Add("booking_id", booking_id.Value);
            }
            if (custid.HasValue)
            {
                where.Add("l.custid = @custid");
                param.Add("custid", custid.Value);
            }
            if (dcode.HasValue)
            {
                where.Add("l.dcode = @dcode");
                param.Add("dcode", dcode.Value);
            }
            if (from_date.HasValue)
            {
                where.Add("l.created_at >= @from_date");
                param.Add("from_date", from_date.Value.ToDateTime(TimeOnly.MinValue));
            }
            if (to_date.HasValue)
            {
                where.Add("l.created_at <= @to_date");
                param.Add("to_date", to_date.Value.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1));
            }
            if (!string.IsNullOrWhiteSpace(action_filter)
                && action_filter.ToUpper() != "ALL")
            {
                where.Add("l.action = @action");
                param.Add("action", action_filter.ToUpper());
            }

            string sql = $@"
    SELECT
        l.log_id,
        l.booking_id,
        l.booking_no,
        l.custid,
        l.dcode,
        l.action,
        l.action_by,
        l.old_slot_detail_id,
        l.new_slot_detail_id,
        l.old_appointment_date,
        l.new_appointment_date,
        l.old_slot_start_time,
        l.new_slot_start_time,
        l.remarks,
        l.tenant_code,                              -- ✅ added
        l.created_at AT TIME ZONE 'UTC' AS created_at,
        ab.booking_status,
        ab.booking_type,
        ab.token_no,
        ab.cancel_reason,
        ab.cancelled_at,
        ab.rescheduled_from,
        NULL::text AS customer_name,
        NULL::text AS mobile,
        dm.name    AS doctor_name
    FROM appointment_booking_log l
    LEFT JOIN appointment_booking ab
           ON ab.booking_id  = l.booking_id
    LEFT JOIN doctor_master dm
           ON dm.dcode       = l.dcode
          AND dm.tenant_code = l.tenant_code
    WHERE {string.Join(" AND ", where)}
    ORDER BY l.created_at DESC";

            var rows = await db.QueryAsync<AppointmentBookingLogViewModel>(sql, param);
            return rows.ToList();
        }
    }
}