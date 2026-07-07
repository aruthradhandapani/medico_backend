using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class WaAppointmentSessionClass
    {
        private readonly string db_conn;

        public WaAppointmentSessionClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<WaAppointmentSessionModel>> Get()
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT sessionid, phonenumber, currentstep, intent, patientname, patientage,
       patientgender, patientcity, departmentid, doctorid,
       slotdate::timestamp AS slotdate,
       slottime, appointmentidref, createdat, updatedat, isactive, language
FROM whatsappdb.wa_appointment_session
ORDER BY sessionid DESC";

            var result = await db.QueryAsync<WaAppointmentSessionModel>(sql);

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY PHONE NUMBER
        // ─────────────────────────────────────────
        public async Task<WaAppointmentSessionModel?> GetByPhoneNumber(string phonenumber)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM public.wa_appointment_session
                WHERE phonenumber = @phonenumber
                AND isactive = true
                ORDER BY sessionid DESC
                LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<WaAppointmentSessionModel>(
                sql,
                new { phonenumber });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(WaAppointmentSessionModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.createdat = DateTime.UtcNow;
                data.updatedat = DateTime.UtcNow;
                data.isactive = data.isactive ?? true;
                data.currentstep = string.IsNullOrWhiteSpace(data.currentstep) ? "WELCOME" : data.currentstep;

                string sql = @"
                    INSERT INTO whatsappdb.wa_appointment_session
                    (
                        phonenumber,
                        currentstep,
                        intent,
                        patientname,
                        patientage,
                        patientgender,
                        patientcity,
                        departmentid,
                        doctorid,
                        slotdate,
                        slottime,
                        appointmentidref,
                        createdat,
                        updatedat,
                        isactive,
                        language
                    )
                    VALUES
                    (
                        @phonenumber,
                        @currentstep,
                        @intent,
                        @patientname,
                        @patientage,
                        @patientgender,
                        @patientcity,
                        @departmentid,
                        @doctorid,
                        @slotdate,
                        @slottime,
                        @appointmentidref,
                        @createdat,
                        @updatedat,
                        @isactive,
                        @language
                    )";

                await db.ExecuteAsync(sql, data);

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}