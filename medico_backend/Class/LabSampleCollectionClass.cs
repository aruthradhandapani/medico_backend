using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    // ══════════════════════════════════════════════════════════════════
    //  Handles:
    //    1. Specimen collection  (collect / accept / reject / resample)
    //    2. Specimen receive     (load received list / mark received)
    //    3. Specimen transfer    (inter-department transfer / status)
    //    4. Patient status       (consolidated review of 1-3 + result)
    // ══════════════════════════════════════════════════════════════════
    public class LabSampleCollectionClass
    {
        private readonly string _conn;

        public LabSampleCollectionClass(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Connection string 'conn' not found.");
        }

        // ══════════════════════════════════════════════════════════════
        // LOAD SAMPLE COLLECTION — request list with collection status
        // ══════════════════════════════════════════════════════════════
        public async Task<(IList<LoadSampleCollectionDto> Data, string? Error)>
            Load_SampleCollection(string tenant_code, DateTime fromdate, DateTime todate, string? status = null)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                var data = await db.QueryAsync<LoadSampleCollectionDto>(@"
                    SELECT DISTINCT ON (lrm.requestguid, tm.scode)
                        lrm.requestguid                 AS RequestGuid,
                        lrm.requestsno                  AS RequestSno,
                        lrm.requestsnoprint             AS RequestSnoprint,
                        lrm.name                        AS PatientName,
                        lrm.gender                      AS Gender,
                        lrm.ageyears                    AS AgeYears,
                        lrm.requestdatetime             AS RequestDateTime,
                        lrm.enteredbhcode               AS Bhcode,
                        tm.scode                        AS Scode,
                        tm.gcode                        AS Gcode,
                        sm.name                         AS SampleName,
                        sm.shortname                    AS SampleShortname,
                        lrsc.lrspid                     AS LrspId,
                        lrsc.barcode                    AS Barcode,
                        lrsc.collectedstatus            AS CollectedStatus,
                        lrsc.collectedtime              AS CollectedTime,
                        lrsc.isaccept                   AS IsAccept,
                        lrsc.isreject                   AS IsReject,
                        lrsc.is_emergency               As IsEmergency,
                        lrsc.is_resampling              AS IsResampling,
                        sl.rejectreason                 AS RejectReason,
                        sl.resamplingreason             AS ResamplingReason
                    FROM   lab_request_master lrm
                    INNER  JOIN lab_request_details lrd
                           ON  lrd.requestguid = lrm.requestguid
                           AND lrd.tenant_code = @tenant_code
                    INNER  JOIN test_master tm
                           ON  tm.tcode        = lrd.tcode
                           AND tm.tenant_code  = @tenant_code
                           AND (tm.deleted = FALSE OR tm.deleted IS NULL)
                    LEFT   JOIN sample_master sm
                           ON  sm.scode        = tm.scode
                           AND sm.tenant_code  = @tenant_code
                           AND (sm.deleted = FALSE OR sm.deleted IS NULL)
                    LEFT   JOIN lab_request_specimencollection lrsc
                           ON  lrsc.requestguid = lrm.requestguid
                           AND lrsc.scode       = tm.scode
                           AND (lrsc.isdeleted = FALSE OR lrsc.isdeleted IS NULL)
                           AND lrsc.tenant_code = @tenant_code
                    -- Latest log per specimen
                    LEFT   JOIN LATERAL (
                               SELECT rejectreason, resamplingreason
                               FROM   lab_request_samplelog
                               WHERE  requestguid  = lrsc.requestguid
                                 AND  scode        = lrsc.scode
                                 AND  tenant_code  = @tenant_code
                               ORDER  BY enteredtime DESC
                               LIMIT  1
                           ) sl ON TRUE
                    WHERE  lrm.tenant_code      = @tenant_code
                      AND  lrm.deleted          = FALSE
                      AND  lrm.requestdatetime >= @fromdate
                      AND  lrm.requestdatetime <  @todate
                    ORDER  BY lrm.requestguid, tm.scode, lrm.requestdatetime DESC",
                    new
                    {
                        tenant_code,
                        fromdate = fromdate.Date,
                        todate = todate.Date.AddDays(1)
                    });

                var list = data.ToList();
                if (!string.IsNullOrEmpty(status))
                {
                    status = status.ToLower().Trim();
                    if (status == "pending")
                    {
                        list = list.Where(x => x.CollectedStatus != true).ToList();
                    }
                    else if (status == "collection")
                    {
                        list = list.Where(x => x.CollectedStatus == true && x.IsAccept != true).ToList();
                    }
                    else if (status == "received" || status == "recieved")
                    {
                        list = list.Where(x => x.IsAccept == true).ToList();
                    }
                }

                return (list, null);
            }
            catch (Exception ex) { return (new List<LoadSampleCollectionDto>(), ex.Message); }
        }

        // ══════════════════════════════════════════════════════════════
        // SAVE SAMPLE COLLECTION  — single upsert
        //   lrspid == Guid.Empty → INSERT (new collection)
        //   lrspid set            → UPDATE (update flags / barcode)
        //
        //   Workflow:
        //     1. Upsert specimencollection (reasons NOT written here)
        //     2. Always insert a new samplelog row (reasons go here)
        //     3. Seed specimenreceive when isaccept flips to true
        //
        //   rejectreason / resamplingreason are passed separately —
        //   they are NEVER stored in specimencollection, only in samplelog.
        // ══════════════════════════════════════════════════════════════
        public async Task<(string Result, Guid? LrspId)>
            Save_SampleCollection(lab_request_specimencollection data,
                                  string? rejectreason,
                                  string? resamplingreason)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);





                // ── DROP PROBLEM TRIGGER CLEANUP ──
                try
                {
                    db.Execute(@"
                        DO $$
                        DECLARE
                            r RECORD;
                        BEGIN
                            FOR r IN (
                                SELECT tgname, relname
                                FROM pg_trigger
                                JOIN pg_class ON pg_class.oid = tgrelid
                                JOIN pg_namespace ON pg_namespace.oid = relnamespace
                                WHERE nspname = 'public' 
                                  AND relname IN ('lab_request_specimencollection', 'lab_request_specimenreceive', 'lab_request_samplelog')
                                  AND NOT tgisinternal
                            ) LOOP
                                EXECUTE 'DROP TRIGGER IF EXISTS ' || quote_ident(r.tgname) || ' ON ' || quote_ident(r.relname) || ' CASCADE';
                            END LOOP;
                        END $$;");
                }
                catch { }



                bool isNew = data.lrspid == Guid.Empty;
                var now = DateTime.UtcNow;

                if (isNew)
                {
                    // ── INSERT ──────────────────────────────────────
                    data.lrspid = Guid.NewGuid();
                    data.isdeleted = false;
                    data.billedtime = now;

                    data.barcode ??= await GenerateBarcode(data, db);

                    if (data.collectedstatus == true && data.collectedtime is null)
                        data.collectedtime = now;

                    if (data.isaccept == true) data.acceptdatetime = now;
                    if (data.isreject == true) data.rejectdatetime = now;
                    if (data.is_resampling == true) data.resamplingdatetime = now;

                    await db.InsertAsync(data);
                }
                else
                {
                    // ── UPDATE ──────────────────────────────────────
                    var existing = await db.QueryFirstOrDefaultAsync<lab_request_specimencollection>(
                        @"SELECT * FROM lab_request_specimencollection
                          WHERE  lrspid      = @lrspid
                            AND  tenant_code = @tenant_code
                            AND  isdeleted   = FALSE",
                        new { data.lrspid, data.tenant_code });

                    if (existing is null) return ("No record found", null);

                    // Preserve immutable fields
                    data.billedtime = existing.billedtime;
                    data.barcode ??= existing.barcode;
                    data.isdeleted = false;

                    // Stamp datetimes only on first transition
                    if (data.collectedstatus == true && existing.collectedstatus != true)
                        data.collectedtime = now;

                    if (data.isaccept == true && existing.isaccept != true)
                        data.acceptdatetime = now;
                    if (data.isreject == true && existing.isreject != true)
                        data.rejectdatetime = now;
                    if (data.is_resampling == true && existing.is_resampling != true)
                        data.resamplingdatetime = now;

                    await db.UpdateAsync(data);
                }

                // Always append a fresh log row — reasons go here, not on specimencollection
                await InsertSampleLog(data, rejectreason, resamplingreason, db);

                // Seed specimenreceive table when specimen is accepted
                if (data.isaccept == true)
                    await SeedSpecimenReceive(data, db);

                return ("Success", data.lrspid);
            }
            catch (Exception ex) { return (ex.Message, null); }
        }

        // ══════════════════════════════════════════════════════════════
        // LOAD SAMPLE RECEIVED
        //   Lists all accepted specimens with their receive status
        //   per department (gcode). date defaults to today.
        // ══════════════════════════════════════════════════════════════
        public async Task<(IList<LoadSampleReceivedDto> Data, string? Error)>
            Load_SampleReceived(string tenant_code, DateTime fromdate, DateTime todate)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                var data = await db.QueryAsync<LoadSampleReceivedDto>(@"
                    SELECT
                        lrsc.lrspid                     AS LrspId,
                        lrsc.requestguid                AS RequestGuid,
                        lrsc.samplereference            AS SampleReference,
                        lrsc.barcode                    AS Barcode,
                        lrsc.collectedtime              AS CollectedTime,
                        lrsc.isaccept                   AS IsAccept,
                        lrsc.acceptdatetime             AS AcceptDateTime,
                        lrsc.isreject                   AS IsReject,
                        sl.rejectreason                 AS RejectReason,
                        lrm.requestsnoprint             AS RequestSnoprint,
                        lrm.name                        AS PatientName,
                        lrm.gender                      AS Gender,
                        lrm.ageyears                    AS AgeYears,
                        lrm.requestdatetime             AS RequestDateTime,                                                
                        lrm.enteredbhcode               AS Bhcode,
                        lrsc.scode                      AS Scode,
                        sm.name                         AS SampleName,
                        sm.shortname                    AS SampleShortname,
                        lrspr.lrsprid                   AS LrsprId,
                        lrspr.receivedstatus            AS ReceivedStatus,
                        lrspr.receivedtime              AS ReceivedTime,
                        lrspr.is_emergency              AS IsEmergency,
                        gm.name                         AS GroupName
                    FROM   lab_request_specimenreceive lrspr
                    INNER  JOIN lab_request_specimencollection lrsc
                           ON  lrsc.requestguid = lrspr.requestguid
                           AND lrsc.scode       = lrspr.scode
                           AND (lrsc.isdeleted = FALSE OR lrsc.isdeleted IS NULL)
                           AND lrsc.tenant_code = @tenant_code
                    INNER  JOIN lab_request_master lrm
                           ON  lrm.requestguid  = lrsc.requestguid
                           AND (lrm.deleted = FALSE OR lrm.deleted IS NULL)
                           AND lrm.tenant_code  = @tenant_code
                    LEFT   JOIN sample_master sm
                           ON  sm.scode         = lrsc.scode
                           AND sm.tenant_code   = @tenant_code
                           AND sm.deleted       = FALSE
                    LEFT   JOIN group_master gm
                           ON  gm.gcode         = lrspr.gcode
                           AND gm.tenant_code   = @tenant_code
                    -- Latest log per specimen for reject reason
                    LEFT   JOIN LATERAL (
                               SELECT rejectreason
                               FROM   lab_request_samplelog
                               WHERE  requestguid  = lrsc.requestguid
                                 AND  scode        = lrsc.scode
                                 AND  tenant_code  = @tenant_code
                               ORDER  BY enteredtime DESC
                               LIMIT  1
                           ) sl ON TRUE
                    WHERE  lrspr.tenant_code     = @tenant_code
                      AND  lrsc.acceptdatetime  >= @fromdate
                      AND  lrsc.acceptdatetime  <  @todate
                    ORDER  BY lrsc.acceptdatetime DESC",
                    new
                    {
                        tenant_code,
                        fromdate = fromdate.Date,
                        todate = todate.Date.AddDays(1)
                    });

                return (data.ToList(), null);
            }
            catch (Exception ex) { return (new List<LoadSampleReceivedDto>(), ex.Message); }
        }

        // ══════════════════════════════════════════════════════════════
        // SAVE SAMPLE RECEIVE — marks a specimenreceive row as received
        //   Also appends a samplelog entry with action_type = RECEIVED
        // ══════════════════════════════════════════════════════════════
        public async Task<string> Save_SampleReceive(SaveSampleReceiveRequest request)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);
                var now = DateTime.UtcNow;

                lab_request_specimencollection? collection = null;
                List<lab_request_specimenreceive> receives = new();

                // 1. Try to treat the passed ID as a collection ID (lrspid)
                collection = await db.QueryFirstOrDefaultAsync<lab_request_specimencollection>(
                    @"SELECT * FROM lab_request_specimencollection
                      WHERE  lrspid      = @lrspid
                        AND  tenant_code = @tenant_code
                        AND  isdeleted   = FALSE",
                    new { lrspid = request.lrsprid, request.tenant_code });

                if (collection != null)
                {
                    // Find all associated receive records
                    receives = (await db.QueryAsync<lab_request_specimenreceive>(
                        @"SELECT * FROM lab_request_specimenreceive
                          WHERE  requestguid = @requestguid
                            AND  scode        = @scode
                            AND  tenant_code  = @tenant_code",
                        new { collection.requestguid, collection.scode, request.tenant_code })).ToList();
                }
                else
                {
                    // 2. Treat the passed ID as a receive ID (lrsprid)
                    var receive = await db.QueryFirstOrDefaultAsync<lab_request_specimenreceive>(
                        @"SELECT * FROM lab_request_specimenreceive
                          WHERE  lrsprid     = @lrsprid
                            AND  tenant_code = @tenant_code",
                        new { lrsprid = request.lrsprid, request.tenant_code });

                    if (receive != null)
                    {
                        receives.Add(receive);

                        // Find the associated collection for logging
                        collection = await db.QueryFirstOrDefaultAsync<lab_request_specimencollection>(
                            @"SELECT * FROM lab_request_specimencollection
                              WHERE  requestguid = @requestguid
                                AND  scode       = @scode
                                AND  tenant_code = @tenant_code
                                AND  isdeleted   = FALSE",
                            new { receive.requestguid, receive.scode, request.tenant_code });
                    }
                }

                if (!receives.Any())
                    return "No record found";

                // Filter to those that aren't received yet
                var pendingReceives = receives.Where(r => r.receivedstatus != true).ToList();
                if (!pendingReceives.Any())
                    return "Already received";

                foreach (var rec in pendingReceives)
                {
                    // Mark as received
                    await db.ExecuteAsync(
                        @"UPDATE lab_request_specimenreceive
                          SET    receivedstatus = TRUE,
                                 receivedtime   = @now
                          WHERE  lrsprid        = @lrsprid
                            AND  tenant_code    = @tenant_code",
                        new { rec.lrsprid, request.tenant_code, now });

                    if (collection != null)
                    {
                        int? tcode = await db.QueryFirstOrDefaultAsync<int?>(@"
                            SELECT tm.tcode
                            FROM   lab_request_details lrd
                            INNER  JOIN test_master tm ON tm.tcode = lrd.tcode AND tm.tenant_code = lrd.tenant_code
                            WHERE  lrd.requestguid = @requestguid
                              AND  tm.scode        = @scode
                              AND  lrd.tenant_code = @tenant_code
                            LIMIT  1",
                            new { requestguid = collection.requestguid, scode = collection.scode, tenant_code = request.tenant_code });

                        // Append a RECEIVED log entry
                        await db.InsertAsync(new lab_request_samplelog
                        {
                            lrslid = Guid.NewGuid(),
                            lrspid = collection.lrspid,
                            requestguid = collection.requestguid,
                            samplereference = collection.samplereference,
                            scode = collection.scode,
                            tcode = tcode,
                            gcode = rec.gcode,
                            action_type = SampleLogAction.Received,
                            billedtime = collection.billedtime,
                            collectedstatus = collection.collectedstatus,
                            collectedtime = collection.collectedtime,
                            barcode = collection.barcode,
                            isaccept = collection.isaccept,
                            acceptdatetime = collection.acceptdatetime,
                            receivedstatus = true,
                            receivedtime = now,
                            enteredtime = now,
                            tenant_code = request.tenant_code
                        });
                    }
                }

                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ══════════════════════════════════════════════════════════════
        // NEW: SAVE SAMPLE TRANSFER — inter-department specimen transfer
        //
        //   action = TRANSFER (default), no lrsptid  → create the row:
        //            dispatch specimen from from_gcode to to_gcode.
        //
        //   action = RECEIVE   + lrsptid → destination dept acknowledges.
        //   action = COMPLETE  + lrsptid → work finished at destination.
        //   action = RETURN    + lrsptid → sent back to source dept.
        //   action = CANCEL    + lrsptid → transfer cancelled.
        //
        //   State machine enforced:
        //     TRANSFER → RECEIVE → COMPLETE
        //                        ↳ RETURN
        //     any non-completed state → CANCEL
        // ══════════════════════════════════════════════════════════════
        public async Task<(string Result, Guid? LrsptId)> Save_SampleTransfer(SaveSampleTransferRequest request)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);
                var now = DateTime.UtcNow;
                var action = string.IsNullOrWhiteSpace(request.action)
                    ? SampleTransferAction.Transfer
                    : request.action.ToUpper().Trim();

                lab_request_specimentransfer? row = null;

                if (request.lrsptid is Guid existingId && existingId != Guid.Empty)
                {
                    row = await db.QueryFirstOrDefaultAsync<lab_request_specimentransfer>(
                        @"SELECT * FROM lab_request_specimentransfer
                          WHERE  lrsptid     = @existingId
                            AND  tenant_code = @tenant_code
                            AND  (deleted = FALSE OR deleted IS NULL)",
                        new { existingId, request.tenant_code });

                    if (row is null) return ("No record found", null);
                }

                // ── Create a brand-new transfer ──
                if (action == SampleTransferAction.Transfer && row is null)
                {
                    if (request.lrspid is null || request.lrspid == Guid.Empty)
                        return ("lrspid is required to start a transfer", null);
                    if (request.from_gcode is null || request.from_gcode == 0)
                        return ("from_gcode is required", null);
                    if (request.to_gcode is null || request.to_gcode == 0)
                        return ("to_gcode is required", null);
                    if (request.from_gcode == request.to_gcode)
                        return ("from_gcode and to_gcode cannot be the same", null);

                    var collection = await db.QueryFirstOrDefaultAsync<lab_request_specimencollection>(
                        @"SELECT * FROM lab_request_specimencollection
                          WHERE  lrspid      = @lrspid
                            AND  tenant_code = @tenant_code
                            AND  (isdeleted = FALSE OR isdeleted IS NULL)",
                        new { request.lrspid, request.tenant_code });

                    if (collection is null) return ("Specimen record not found", null);

                    row = new lab_request_specimentransfer
                    {
                        lrsptid = Guid.NewGuid(),
                        lrspid = collection.lrspid,
                        requestguid = collection.requestguid,
                        samplereference = collection.samplereference,
                        defaults_code = collection.defaults_code,
                        scode = collection.scode,
                        barcode = collection.barcode,
                        from_gcode = request.from_gcode.Value,
                        to_gcode = request.to_gcode.Value,
                        transferstatus = true,
                        transferdatetime = now,
                        receivedstatus = false,
                        completedstatus = false,
                        returnstatus = false,
                        cancelledstatus = false,
                        priority = string.IsNullOrWhiteSpace(request.priority) ? "Normal" : request.priority,
                        transferreason = request.transferreason,
                        remarks = request.remarks,
                        transferredby = request.usercode,
                        is_emergency = request.is_emergency ?? collection.is_emergency,
                        deleted = false,
                        tenant_code = request.tenant_code,
                        createdby = request.usercode,
                        createdon = now
                    };

                    await db.InsertAsync(row);
                    return ("Success", row.lrsptid);
                }

                if (row is null) return ("lrsptid is required for this action", null);

                // ── Advance the state of an existing transfer ──
                switch (action)
                {
                    case SampleTransferAction.Receive:
                        if (row.transferstatus != true) return ("Specimen has not been transferred yet", null);
                        if (row.cancelledstatus == true) return ("Transfer was cancelled", null);
                        if (row.receivedstatus == true) return ("Transfer already received", null);
                        row.receivedstatus = true;
                        row.receiveddatetime = now;
                        row.receivedby = request.usercode;
                        break;

                    case SampleTransferAction.Complete:
                        if (row.receivedstatus != true) return ("Specimen has not been received at destination yet", null);
                        if (row.completedstatus == true) return ("Transfer already completed", null);
                        row.completedstatus = true;
                        row.completeddatetime = now;
                        row.completedby = request.usercode;
                        break;

                    case SampleTransferAction.Return:
                        if (row.receivedstatus != true) return ("Only a received specimen can be returned", null);
                        if (row.returnstatus == true) return ("Transfer already returned", null);
                        row.returnstatus = true;
                        row.returndatetime = now;
                        break;

                    case SampleTransferAction.Cancel:
                        if (row.completedstatus == true) return ("Completed transfer cannot be cancelled", null);
                        if (row.cancelledstatus == true) return ("Transfer already cancelled", null);
                        row.cancelledstatus = true;
                        row.cancelleddatetime = now;
                        break;

                    default:
                        return ($"Unknown action '{action}'", null);
                }

                if (!string.IsNullOrWhiteSpace(request.remarks)) row.remarks = request.remarks;
                row.modifiedby = request.usercode;
                row.modifiedon = now;

                await db.UpdateAsync(row);
                return ("Success", row.lrsptid);
            }
            catch (Exception ex) { return (ex.Message, null); }
        }

        // ══════════════════════════════════════════════════════════════
        // NEW: LOAD PATIENT STATUS — consolidated review by requestguid
        //
        //   Per test on the request, returns:
        //     • collection status  (collected / accepted / rejected / resampling)
        //     • every receive row  (received at this gcode, with time)
        //     • every transfer row (from → to gcode, full state, with usercodes)
        //     • result status      (entered / authorize1 / authorize2, with usercodes)
        // ══════════════════════════════════════════════════════════════
        public async Task<(PatientStatusDto? Data, string? Error)> Load_PatientStatus(string tenant_code, Guid requestguid)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                var patient = await db.QueryFirstOrDefaultAsync<PatientStatusDto>(@"
                    SELECT
                        lrm.requestguid       AS RequestGuid,
                        lrm.requestsnoprint   AS RequestSnoprint,
                        lrm.name              AS PatientName,
                        lrm.gender            AS Gender,
                        lrm.ageyears          AS AgeYears,
                        lrm.requestdatetime   AS RequestDateTime,
                        lrm.enteredbhcode     AS Bhcode
                    FROM   lab_request_master lrm
                    WHERE  lrm.requestguid  = @requestguid
                      AND  lrm.tenant_code  = @tenant_code
                      AND  (lrm.deleted = FALSE OR lrm.deleted IS NULL)",
                    new { requestguid, tenant_code });

                if (patient is null) return (null, "Request not found");

                // Test list for this request
                var tests = (await db.QueryAsync<TestStatusDto>(@"
                    SELECT
                        tm.tcode     AS Tcode,
                        tm.name      AS TestName,
                        tm.scode     AS Scode,
                        sm.name      AS SampleName,
                        tm.gcode     AS Gcode,
                        gm.name      AS GroupName
                    FROM   lab_request_details lrd
                    INNER  JOIN test_master tm ON tm.tcode = lrd.tcode AND tm.tenant_code = lrd.tenant_code
                    LEFT   JOIN sample_master sm ON sm.scode = tm.scode AND sm.tenant_code = lrd.tenant_code
                    LEFT   JOIN group_master gm ON gm.gcode = tm.gcode AND gm.tenant_code = lrd.tenant_code
                    WHERE  lrd.requestguid = @requestguid
                      AND  lrd.tenant_code = @tenant_code",
                    new { requestguid, tenant_code })).ToList();

                // Collection rows (one per scode)
                var collections = (await db.QueryAsync<lab_request_specimencollection>(@"
                    SELECT * FROM lab_request_specimencollection
                    WHERE  requestguid = @requestguid
                      AND  tenant_code = @tenant_code
                      AND  (isdeleted = FALSE OR isdeleted IS NULL)",
                    new { requestguid, tenant_code })).ToList();

                // Latest reject/resampling reason per scode from samplelog
                var reasons = (await db.QueryAsync<lab_request_samplelog>(@"
                    SELECT DISTINCT ON (scode) *
                    FROM   lab_request_samplelog
                    WHERE  requestguid = @requestguid
                      AND  tenant_code = @tenant_code
                    ORDER  BY scode, enteredtime DESC",
                    new { requestguid, tenant_code })).ToList();

                // Receive rows (one per scode + gcode)
                var receiveRows = (await db.QueryAsync<ReceiveRow>(@"
                    SELECT
                        lrspr.lrsprid        AS lrsprid,
                        lrspr.scode          AS scode,
                        lrspr.gcode          AS gcode,
                        gm.name              AS GroupName,
                        lrspr.receivedstatus AS receivedstatus,
                        lrspr.receivedtime   AS receivedtime
                    FROM   lab_request_specimenreceive lrspr
                    LEFT   JOIN group_master gm ON gm.gcode = lrspr.gcode AND gm.tenant_code = lrspr.tenant_code
                    WHERE  lrspr.requestguid = @requestguid
                      AND  lrspr.tenant_code = @tenant_code",
                    new { requestguid, tenant_code })).ToList();

                // Transfer rows (one per transfer event, scode + from/to gcode)
                var transferRows = (await db.QueryAsync<TransferRow>(@"
                    SELECT
                        lrspt.lrsptid           AS lrsptid,
                        lrspt.scode             AS scode,
                        lrspt.from_gcode        AS from_gcode,
                        fgm.name                AS FromGroupName,
                        lrspt.to_gcode          AS to_gcode,
                        tgm.name                AS ToGroupName,
                        lrspt.transferstatus    AS transferstatus,
                        lrspt.transferdatetime  AS transferdatetime,
                        lrspt.transferredby     AS transferredby,
                        lrspt.receivedstatus    AS receivedstatus,
                        lrspt.receiveddatetime  AS receiveddatetime,
                        lrspt.receivedby        AS receivedby,
                        lrspt.completedstatus   AS completedstatus,
                        lrspt.completeddatetime AS completeddatetime,
                        lrspt.completedby       AS completedby,
                        lrspt.returnstatus      AS returnstatus,
                        lrspt.returndatetime    AS returndatetime,
                        lrspt.cancelledstatus   AS cancelledstatus,
                        lrspt.cancelleddatetime AS cancelleddatetime,
                        lrspt.priority          AS priority,
                        lrspt.transferreason    AS transferreason,
                        lrspt.remarks           AS remarks
                    FROM   lab_request_specimentransfer lrspt
                    LEFT   JOIN group_master fgm ON fgm.gcode = lrspt.from_gcode AND fgm.tenant_code = lrspt.tenant_code
                    LEFT   JOIN group_master tgm ON tgm.gcode = lrspt.to_gcode   AND tgm.tenant_code = lrspt.tenant_code
                    WHERE  lrspt.requestguid = @requestguid
                      AND  lrspt.tenant_code = @tenant_code
                      AND  (lrspt.deleted = FALSE OR lrspt.deleted IS NULL)
                    ORDER  BY lrspt.transferdatetime",
                    new { requestguid, tenant_code })).ToList();

                // Result status per test (tcode) — entry / authorize1 / authorize2 with usercodes
                var resultRows = (await db.QueryAsync<ResultRow>(@"
                    SELECT
                        lrd.tcode                AS tcode,
                        lrd.resultstatus         AS resultstatus,
                        lrd.resultenteredby      AS resultenteredby,
                        lrd.resultentereddate    AS resultentereddate,
                        lrd.isauthorized1        AS isauthorized1,
                        lrd.resultauthorizedby   AS resultauthorizedby,
                        lrd.firstauthorizedate   AS firstauthorizedate,
                        lrd.isauthorized2        AS isauthorized2,
                        lrd.resultauthorizedby2  AS resultauthorizedby2,
                        lrd.secondauthorizedate  AS secondauthorizedate
                    FROM   lab_request_details lrd
                    WHERE  lrd.requestguid = @requestguid
                      AND  lrd.tenant_code = @tenant_code",
                    new { requestguid, tenant_code })).ToList();

                // ── Assemble per test ──
                foreach (var test in tests)
                {
                    var collection = collections.FirstOrDefault(c => c.scode == test.Scode);
                    if (collection != null)
                    {
                        var reasonLog = reasons.FirstOrDefault(r => r.scode == test.Scode);
                        test.Collection = new CollectionStatusDetail
                        {
                            LrspId = collection.lrspid,
                            Barcode = collection.barcode,
                            CollectedStatus = collection.collectedstatus,
                            CollectedTime = collection.collectedtime,
                            IsAccept = collection.isaccept,
                            AcceptDateTime = collection.acceptdatetime,
                            IsReject = collection.isreject,
                            RejectDateTime = collection.rejectdatetime,
                            RejectReason = reasonLog?.rejectreason,
                            IsResampling = collection.is_resampling,
                            ResamplingDateTime = collection.resamplingdatetime,
                            ResamplingReason = reasonLog?.resamplingreason
                        };
                    }

                    test.Receives = receiveRows
                        .Where(r => r.scode == test.Scode)
                        .Select(r => new ReceiveStatusDetail
                        {
                            LrsprId = r.lrsprid,
                            Gcode = r.gcode,
                            GroupName = r.GroupName,
                            ReceivedStatus = r.receivedstatus,
                            ReceivedTime = r.receivedtime
                        }).ToList();

                    test.Transfers = transferRows
                        .Where(t => t.scode == test.Scode)
                        .Select(t => new TransferStatusDetail
                        {
                            LrsptId = t.lrsptid,
                            FromGcode = t.from_gcode,
                            FromGroupName = t.FromGroupName,
                            ToGcode = t.to_gcode,
                            ToGroupName = t.ToGroupName,
                            TransferStatus = t.transferstatus,
                            TransferDateTime = t.transferdatetime,
                            TransferredBy = t.transferredby,
                            ReceivedStatus = t.receivedstatus,
                            ReceivedDateTime = t.receiveddatetime,
                            ReceivedBy = t.receivedby,
                            CompletedStatus = t.completedstatus,
                            CompletedDateTime = t.completeddatetime,
                            CompletedBy = t.completedby,
                            ReturnStatus = t.returnstatus,
                            ReturnDateTime = t.returndatetime,
                            CancelledStatus = t.cancelledstatus,
                            CancelledDateTime = t.cancelleddatetime,
                            Priority = t.priority,
                            TransferReason = t.transferreason,
                            Remarks = t.remarks
                        }).ToList();

                    var result = resultRows.FirstOrDefault(r => r.tcode == test.Tcode);
                    if (result != null)
                    {
                        test.Result = new ResultStatusDetail
                        {
                            ResultStatus = result.resultstatus,
                            ResultEnteredBy = result.resultenteredby,
                            ResultEnteredDate = result.resultentereddate,
                            IsAuthorized1 = result.isauthorized1,
                            ResultAuthorizedBy = result.resultauthorizedby,
                            FirstAuthorizeDate = result.firstauthorizedate,
                            IsAuthorized2 = result.isauthorized2,
                            ResultAuthorizedBy2 = result.resultauthorizedby2,
                            SecondAuthorizeDate = result.secondauthorizedate
                        };
                    }
                }

                patient.Tests = tests;
                return (patient, null);
            }
            catch (Exception ex) { return (null, ex.Message); }
        }

        // ══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════

        private static string DeriveActionType(lab_request_specimencollection c) =>
            c.isreject == true ? SampleLogAction.Rejected :
            c.is_resampling == true ? SampleLogAction.Resampling :
            c.isaccept == true ? SampleLogAction.Accepted :
                                      SampleLogAction.Collected;

        /// <summary>
        /// Appends a samplelog row. Reasons are passed separately
        /// because they are NOT stored on the specimencollection model.
        /// </summary>
        private static async Task InsertSampleLog(
            lab_request_specimencollection c,
            string? rejectreason,
            string? resamplingreason,
            IDbConnection db)
        {
            int? tcode = await db.QueryFirstOrDefaultAsync<int?>(@"
                SELECT tm.tcode
                FROM   lab_request_details lrd
                INNER  JOIN test_master tm ON tm.tcode = lrd.tcode AND tm.tenant_code = lrd.tenant_code
                WHERE  lrd.requestguid = @requestguid
                  AND  tm.scode        = @scode
                  AND  lrd.tenant_code = @tenant_code
                LIMIT  1",
                new { c.requestguid, c.scode, c.tenant_code });

            await db.InsertAsync(new lab_request_samplelog
            {
                lrslid = Guid.NewGuid(),
                lrspid = c.lrspid,
                requestguid = c.requestguid,
                samplereference = c.samplereference,
                scode = c.scode,
                tcode = tcode,
                action_type = DeriveActionType(c),
                billedtime = c.billedtime,
                collectedstatus = c.collectedstatus,
                collectedtime = c.collectedtime,
                barcode = c.barcode,
                isaccept = c.isaccept,
                acceptdatetime = c.acceptdatetime,
                isreject = c.isreject,
                rejectreason = rejectreason,
                rejectdatetime = c.rejectdatetime,
                is_resampling = c.is_resampling,
                resamplingreason = resamplingreason,
                resamplingdatetime = c.resamplingdatetime,
                enteredtime = DateTime.UtcNow,
                tenant_code = c.tenant_code
            });
        }

        // Seeds specimenreceive — one row per department (gcode)
        private static async Task SeedSpecimenReceive(lab_request_specimencollection c, IDbConnection db)
        {
            var gcodes = (await db.QueryAsync<int>(
                @"SELECT DISTINCT tm.gcode
                  FROM   lab_request_details lrd
                  INNER  JOIN test_master tm ON tm.tcode = lrd.tcode AND tm.tenant_code = lrd.tenant_code
                  WHERE  lrd.requestguid = @requestguid
                    AND  tm.scode        = @scode
                    AND  tm.gcode        IS NOT NULL
                    AND  lrd.tenant_code = @tenant_code",
                new { requestguid = c.requestguid, scode = c.scode, tenant_code = c.tenant_code })).ToList();

            if (!gcodes.Any())
            {
                await InsertReceiveRowIfMissing(c, db, gcode: null, allDepartment: true);
            }
            else
            {
                foreach (var gcode in gcodes)
                    await InsertReceiveRowIfMissing(c, db, gcode, allDepartment: false);
            }
        }

        private static async Task InsertReceiveRowIfMissing(
            lab_request_specimencollection c, IDbConnection db,
            int? gcode, bool allDepartment)
        {
            bool exists = await db.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS (
                      SELECT 1 FROM lab_request_specimenreceive
                      WHERE  requestguid = @requestguid
                        AND  scode       = @scode
                        AND  (gcode = @gcode OR (@gcode IS NULL AND gcode IS NULL))
                        AND  tenant_code = @tenant_code
                  )",
                new { c.requestguid, c.scode, gcode, c.tenant_code });

            if (exists) return;

            await db.InsertAsync(new lab_request_specimenreceive
            {
                lrsprid = Guid.NewGuid(),
                lrspid = c.lrspid,
                requestguid = c.requestguid,
                samplereference = c.samplereference,
                scode = c.scode,
                defaults_code = c.scode,
                billedtime = c.billedtime,
                collectedtime = c.collectedtime,
                collectedstatus = c.collectedstatus,
                changedmanually = c.changedmanually,
                gcode = gcode,
                receivedstatus = false,
                alldepartment = allDepartment,
                is_emergency = c.is_emergency,
                tenant_code = c.tenant_code
            });
        }

        // ══════════════════════════════════════════════════════════════
        // LOAD SAMPLE TRANSFER — dept work queue / full transfer history
        //
        //   gcode + direction:
        //     direction = "outgoing" → transfers dispatched FROM gcode
        //     direction = "incoming" → transfers dispatched TO   gcode
        //     direction omitted      → either side matches gcode
        //
        //   status:
        //     pending    → transferstatus = true, not yet received, not cancelled
        //     received   → receivedstatus = true, not yet completed
        //     completed  → completedstatus = true
        //     returned   → returnstatus = true
        //     cancelled  → cancelledstatus = true
        // ══════════════════════════════════════════════════════════════
        public async Task<(IList<LoadSampleTransferDto> Data, string? Error)>
            Load_SampleTransfer(string tenant_code, DateTime fromdate, DateTime todate,
                                Guid? requestguid = null, int? gcode = null,
                                string? direction = null, string? status = null)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_conn);

                var data = await db.QueryAsync<LoadSampleTransferDto>(@"
            SELECT
                lrspt.lrsptid           AS LrsptId,
                lrspt.lrspid            AS LrspId,
                lrspt.requestguid       AS RequestGuid,
                lrspt.barcode           AS Barcode,
                lrspt.scode             AS Scode,
                sm.name                 AS SampleName,
                sm.shortname            AS SampleShortname,

                lrm.requestsnoprint     AS RequestSnoprint,
                lrm.name                AS PatientName,
                lrm.gender              AS Gender,
                lrm.ageyears            AS AgeYears,
                lrm.requestdatetime     AS RequestDateTime,
                lrm.enteredbhcode       AS Bhcode,

                lrspt.from_gcode        AS FromGcode,
                fgm.name                AS FromGroupName,
                lrspt.to_gcode          AS ToGcode,
                tgm.name                AS ToGroupName,

                lrspt.transferstatus    AS TransferStatus,
                lrspt.transferdatetime  AS TransferDateTime,
                lrspt.transferredby     AS TransferredBy,

                lrspt.receivedstatus    AS ReceivedStatus,
                lrspt.receiveddatetime  AS ReceivedDateTime,
                lrspt.receivedby        AS ReceivedBy,

                lrspt.completedstatus   AS CompletedStatus,
                lrspt.completeddatetime AS CompletedDateTime,
                lrspt.completedby       AS CompletedBy,

                lrspt.returnstatus      AS ReturnStatus,
                lrspt.returndatetime    AS ReturnDateTime,

                lrspt.cancelledstatus   AS CancelledStatus,
                lrspt.cancelleddatetime AS CancelledDateTime,

                lrspt.priority          AS Priority,
                lrspt.transferreason    AS TransferReason,
                lrspt.remarks           AS Remarks,
                lrspt.is_emergency      AS IsEmergency
            FROM   lab_request_specimentransfer lrspt
            INNER  JOIN lab_request_master lrm
                   ON  lrm.requestguid  = lrspt.requestguid
                   AND lrm.tenant_code  = @tenant_code
                   AND (lrm.deleted = FALSE OR lrm.deleted IS NULL)
            LEFT   JOIN sample_master sm
                   ON  sm.scode        = lrspt.scode
                   AND sm.tenant_code  = @tenant_code
                   AND (sm.deleted = FALSE OR sm.deleted IS NULL)
            LEFT   JOIN group_master fgm ON fgm.gcode = lrspt.from_gcode AND fgm.tenant_code = @tenant_code
            LEFT   JOIN group_master tgm ON tgm.gcode = lrspt.to_gcode   AND tgm.tenant_code = @tenant_code
            WHERE  lrspt.tenant_code      = @tenant_code
              AND  (lrspt.deleted = FALSE OR lrspt.deleted IS NULL)
              AND  lrspt.transferdatetime >= @fromdate
              AND  lrspt.transferdatetime <  @todate
              AND  (@requestguid::uuid IS NULL OR lrspt.requestguid = @requestguid)
              AND  (
                    @gcode::int IS NULL
                    OR (@direction = 'outgoing' AND lrspt.from_gcode = @gcode)
                    OR (@direction = 'incoming' AND lrspt.to_gcode   = @gcode)
                    OR (@direction IS NULL AND (lrspt.from_gcode = @gcode OR lrspt.to_gcode = @gcode))
                   )
            ORDER  BY lrspt.transferdatetime DESC",
                    new
                    {
                        tenant_code,
                        fromdate = fromdate.Date,
                        todate = todate.Date.AddDays(1),
                        requestguid,
                        gcode,
                        direction = string.IsNullOrWhiteSpace(direction) ? null : direction.ToLower().Trim()
                    });

                var list = data.ToList();
                if (!string.IsNullOrEmpty(status))
                {
                    status = status.ToLower().Trim();
                    list = status switch
                    {
                        "pending" => list.Where(x => x.TransferStatus == true
                                                   && x.ReceivedStatus != true
                                                   && x.CancelledStatus != true).ToList(),
                        "received" => list.Where(x => x.ReceivedStatus == true
                                                    && x.CompletedStatus != true).ToList(),
                        "completed" => list.Where(x => x.CompletedStatus == true).ToList(),
                        "returned" => list.Where(x => x.ReturnStatus == true).ToList(),
                        "cancelled" => list.Where(x => x.CancelledStatus == true).ToList(),
                        _ => list
                    };
                }

                return (list, null);
            }
            catch (Exception ex) { return (new List<LoadSampleTransferDto>(), ex.Message); }
        }

        private static async Task<string?> GenerateBarcode(
            lab_request_specimencollection c, IDbConnection db)
        {
            return await db.QueryFirstOrDefaultAsync<string>(
                @"SELECT lrm.requestsnoprint
                  FROM   lab_request_master lrm
                  WHERE  lrm.requestguid  = @requestguid
                    AND  lrm.tenant_code  = @tenant_code
                  LIMIT  1",
                new { c.requestguid, c.tenant_code });
        }

        // ── Row-shaped helpers for Load_PatientStatus (join projections) ──
        private class ReceiveRow
        {
            public Guid lrsprid { get; set; }
            public int? scode { get; set; }
            public int? gcode { get; set; }
            public string? GroupName { get; set; }
            public bool? receivedstatus { get; set; }
            public DateTime? receivedtime { get; set; }
        }

        private class TransferRow
        {
            public Guid lrsptid { get; set; }
            public int? scode { get; set; }
            public int from_gcode { get; set; }
            public string? FromGroupName { get; set; }
            public int to_gcode { get; set; }
            public string? ToGroupName { get; set; }
            public bool? transferstatus { get; set; }
            public DateTime? transferdatetime { get; set; }
            public long? transferredby { get; set; }
            public bool? receivedstatus { get; set; }
            public DateTime? receiveddatetime { get; set; }
            public long? receivedby { get; set; }
            public bool? completedstatus { get; set; }
            public DateTime? completeddatetime { get; set; }
            public long? completedby { get; set; }
            public bool? returnstatus { get; set; }
            public DateTime? returndatetime { get; set; }
            public bool? cancelledstatus { get; set; }
            public DateTime? cancelleddatetime { get; set; }
            public string? priority { get; set; }
            public string? transferreason { get; set; }
            public string? remarks { get; set; }
        }

        private class ResultRow
        {
            public int? tcode { get; set; }
            public bool? resultstatus { get; set; }
            public int? resultenteredby { get; set; }
            public DateTimeOffset? resultentereddate { get; set; }
            public bool? isauthorized1 { get; set; }
            public int? resultauthorizedby { get; set; }
            public DateTimeOffset? firstauthorizedate { get; set; }
            public bool? isauthorized2 { get; set; }
            public int? resultauthorizedby2 { get; set; }
            public DateTimeOffset? secondauthorizedate { get; set; }
        }
    }
}