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

                // ✅ Validate booking_type
                var allowedTypes = new[] { "WALKIN", "ONLINE" };
                if (!allowedTypes.Contains(data.booking_type.ToUpper()))
                    return "Invalid booking_type. Allowed: WALKIN, ONLINE";

                data.booking_type = data.booking_type.ToUpper();

                // ✅ Fetch slot and check limits based on booking_type
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

                if (data.booking_type == "ONLINE" && slot.online_count >= slot.max_online)
                    return "Online booking limit reached for this slot";

                data.booking_id = Guid.NewGuid();
                data.token_no = await GetNextToken(data.slot_detail_id, data.tenant_code!);
                data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                // ✅ Only set BOOKED if not already RESCHEDULED
                if (data.booking_status != "RESCHEDULED")
                    data.booking_status = "BOOKED";

                string insertSql = @"INSERT INTO appointment_booking
                        (booking_id, custid, dcode, slot_detail_id, slot_master_id,
                         appointment_date, slot_start_time, slot_end_time,
                         token_no, booking_status, booking_type, notes,
                         rescheduled_from, reschedule_reason,
                         tenant_code, isdeleted, created_at, updated_at)
                       VALUES
                        (@booking_id, @custid, @dcode, @slot_detail_id, @slot_master_id,
                         @appointment_date, @slot_start_time, @slot_end_time,
                         @token_no, @booking_status, @booking_type, @notes,
                         @rescheduled_from, @reschedule_reason,
                         @tenant_code, @isdeleted, @created_at, @updated_at)";

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
                    data.booking_status,
                    data.booking_type,
                    data.notes,
                    data.rescheduled_from,
                    data.reschedule_reason,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

                // ✅ Increment correct counter based on booking_type
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

                return $"Success|Token:{data.token_no}|BookingId:{data.booking_id}";
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

                // ✅ Mark booking as cancelled
                string cancelSql = @"UPDATE appointment_booking
                                     SET booking_status = 'CANCELLED',
                                         cancel_reason  = @cancel_reason,
                                         cancelled_at   = now(),
                                         updated_at     = now()
                                     WHERE booking_id  = @booking_id
                                     AND   tenant_code = @tenant_code";

                await db.ExecuteAsync(cancelSql, new { booking_id, cancel_reason, tenant_code });

                // ✅ Decrement correct counter + reopen slot if it was FULL
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
        // RESCHEDULE APPOINTMENT
        // ─────────────────────────────────────────
        public async Task<string> RescheduleAppointment(
            RescheduleAppointmentRequest request, string tenant_code)
        {
            try
            {
                // ✅ Cancel old booking — this decrements old slot counters correctly
                var cancelResult = await CancelAppointment(
                    request.old_booking_id, "Rescheduled", tenant_code);

                if (cancelResult != "Cancelled Successfully")
                    return $"Cancel failed: {cancelResult}";

                // ✅ Carry forward booking_type to new booking
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

                var allowed = new[] { "BOOKED", "CONFIRMED", "VISITED", "CANCELLED", "RESCHEDULE_PENDING" };
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

                // ✅ Check old booking is RESCHEDULE_PENDING
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

                // ✅ Cancel old booking
                var cancelResult = await CancelAppointment(
                    request.old_booking_id,
                    "Rescheduled — whole slot moved",
                    tenant_code);

                if (cancelResult != "Cancelled Successfully")
                    return $"Cancel failed: {cancelResult}";

                // ✅ Build and book new appointment
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

        public async Task<object> RescheduleWholeSlot(
     RescheduleWholeSlotRequest request, string tenant_code)
        {
            var rescheduled = new List<string>();
            var failed = new List<string>();

            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                // ✅ STEP 1 — Validate new slot
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

                // ✅ STEP 1.5 — Auto mark all active bookings as RESCHEDULE_PENDING
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

                // ✅ STEP 2 — Get all RESCHEDULE_PENDING bookings
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

                // ✅ STEP 3 — Check capacity
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

                // ✅ STEP 4 — Reschedule each patient
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
    }
}