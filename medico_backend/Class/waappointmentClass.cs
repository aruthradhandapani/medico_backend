using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace medico_backend.Class
{
    public class WaAppointmentSessionClass
    {
        private readonly string _conn;
        private readonly ILogger<WaAppointmentSessionClass> _logger;

        public WaAppointmentSessionClass(IConfiguration cfg, ILogger<WaAppointmentSessionClass> logger)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Database connection string 'conn' not found.");
            _logger = logger;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ── Get active session for a phone number (or null) ──────────
        public async Task<WaAppointmentSession?> GetActiveSessionByPhone(string phonenumber)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<WaAppointmentSession>(
                @"SELECT * FROM wa_appointment_session
                  WHERE phonenumber = @phonenumber AND isactive = true
                  ORDER BY createdat DESC
                  LIMIT 1",
                new { phonenumber });
        }

        // ── Get by sessionid ──────────────────────────────────────────
        public async Task<WaAppointmentSession?> GetById(int sessionid)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<WaAppointmentSession>(
                "SELECT * FROM wa_appointment_session WHERE sessionid = @sessionid",
                new { sessionid });
        }

        // ── Create a new session (closes any existing active one first) ──
        public async Task<(string status, WaAppointmentSession? data)> CreateSession(CreateWaSessionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.phonenumber))
                return ("Phone number is required.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Deactivate any existing active session for this phone number
                await db.ExecuteAsync(
                    @"UPDATE wa_appointment_session
                      SET isactive = false, updatedat = @now
                      WHERE phonenumber = @phonenumber AND isactive = true",
                    new { phonenumber = req.phonenumber, now = DateTime.UtcNow }, tx);

                var session = new WaAppointmentSession
                {
                    phonenumber = req.phonenumber,
                    currentstep = string.IsNullOrWhiteSpace(req.currentstep) ? "WELCOME" : req.currentstep,
                    intent = req.intent,
                    language = req.language,
                    createdat = DateTime.UtcNow,
                    updatedat = DateTime.UtcNow,
                    isactive = true
                };

                var newId = await db.InsertAsync(session, tx);
                session.sessionid = newId;

                tx.Commit();
                return ("SUCCESS", session);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to create WA appointment session.");
                return ($"Internal transaction error: {ex.Message}", null);
            }
        }

        // ── Update an existing session (partial update, only sets provided fields) ──
        public async Task<(string status, WaAppointmentSession? data)> UpdateSession(UpdateWaSessionRequest req)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existing = await db.QueryFirstOrDefaultAsync<WaAppointmentSession>(
                    "SELECT * FROM wa_appointment_session WHERE sessionid = @sessionid FOR UPDATE",
                    new { req.sessionid }, tx);

                if (existing == null) return ("Session not found.", null);

                existing.currentstep = req.currentstep ?? existing.currentstep;
                existing.intent = req.intent ?? existing.intent;
                existing.patientname = req.patientname ?? existing.patientname;
                existing.patientage = req.patientage ?? existing.patientage;
                existing.patientgender = req.patientgender ?? existing.patientgender;
                existing.patientcity = req.patientcity ?? existing.patientcity;
                existing.departmentid = req.departmentid ?? existing.departmentid;
                existing.doctorid = req.doctorid ?? existing.doctorid;
                existing.slotdate = req.slotdate ?? existing.slotdate;
                existing.slottime = req.slottime ?? existing.slottime;
                existing.appointmentidref = req.appointmentidref ?? existing.appointmentidref;
                existing.language = req.language ?? existing.language;
                existing.updatedat = DateTime.UtcNow;

                await db.UpdateAsync(existing, tx);
                tx.Commit();

                return ("SUCCESS", existing);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to update WA appointment session {sessionid}", req.sessionid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ── Close (deactivate) a session ──────────────────────────────
        public async Task<string> CloseSession(int sessionid)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<WaAppointmentSession>(
                "SELECT * FROM wa_appointment_session WHERE sessionid = @sessionid",
                new { sessionid });

            if (existing == null) return "Session not found.";
            if (existing.isactive == false) return "Session is already inactive.";

            await db.ExecuteAsync(
                @"UPDATE wa_appointment_session
                  SET isactive = false, updatedat = @now
                  WHERE sessionid = @sessionid",
                new { sessionid, now = DateTime.UtcNow });

            return "SUCCESS";
        }

    }
}