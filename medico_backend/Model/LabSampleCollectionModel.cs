using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;

namespace medico_backend.Model
{
    public static class SampleLogAction
    {
        public const string Collected = "COLLECTED";
        public const string Accepted = "ACCEPTED";
        public const string Rejected = "REJECTED";
        public const string Resampling = "RESAMPLING";
        public const string Received = "RECEIVED";
    }

    // ── NEW: actions accepted by the sampletransfer endpoint ──────────
    public static class SampleTransferAction
    {
        public const string Transfer = "TRANSFER";   // dispatch from source dept
        public const string Receive = "RECEIVE";     // acknowledged at destination dept
        public const string Complete = "COMPLETE";   // work finished at destination dept
        public const string Return = "RETURN";       // sent back to source dept
        public const string Cancel = "CANCEL";       // transfer cancelled
    }


    [Table("lab_request_specimencollection")]
    public class lab_request_specimencollection
    {
        [ExplicitKey]
        public Guid lrspid { get; set; }

        public Guid? requestguid { get; set; }
        public string? samplereference { get; set; }
        public int? sno { get; set; }
        public int? defaults_code { get; set; }
        public int? scode { get; set; }
        public int? gcode { get; set; }
        public DateTime? billedtime { get; set; }
        public DateTime? collectedtime { get; set; }
        public bool? collectedstatus { get; set; }
        public bool? changedmanually { get; set; }
        public int? ctcode { get; set; }
        public string? barcode { get; set; }

        public bool? isaccept { get; set; }
        public DateTime? acceptdatetime { get; set; }

        public bool? isreject { get; set; }
        public DateTime? rejectdatetime { get; set; }

        public bool? is_resampling { get; set; }
        public DateTime? resamplingdatetime { get; set; }

        public bool? isdeleted { get; set; }
        public DateTime? deletedtime { get; set; }
        public bool? is_emergency { get; set; }
        public string? tenant_code { get; set; }
    }


    [Table("lab_request_samplelog")]
    public class lab_request_samplelog
    {
        [ExplicitKey]
        public Guid lrslid { get; set; }
        public Guid lrspid { get; set; }

        public Guid? requestguid { get; set; }
        public string? samplereference { get; set; }
        public int? scode { get; set; }
        public int? tcode { get; set; }
        public int? gcode { get; set; }

        // ── FIX: was missing; written by DeriveActionType ─────────
        [Write(false)]
        public string? action_type { get; set; }

        public DateTime? billedtime { get; set; }
        public bool? collectedstatus { get; set; }
        public DateTime? collectedtime { get; set; }
        public bool? receivedstatus { get; set; }
        public DateTime? receivedtime { get; set; }
        public bool? enteredstatus { get; set; }
        public DateTime? enteredtime { get; set; }
        public string? barcode { get; set; }

        public bool? isaccept { get; set; }
        public DateTime? acceptdatetime { get; set; }

        public bool? isreject { get; set; }
        public string? rejectreason { get; set; }
        public DateTime? rejectdatetime { get; set; }

        public bool? is_resampling { get; set; }
        public string? resamplingreason { get; set; }
        public DateTime? resamplingdatetime { get; set; }

        public string? tenant_code { get; set; }
    }


    [Table("lab_request_specimenreceive")]
    public class lab_request_specimenreceive
    {
        [ExplicitKey]
        public Guid lrsprid { get; set; }
        public Guid lrspid { get; set; }

        public Guid? requestguid { get; set; }
        public string? samplereference { get; set; }
        public int? defaults_code { get; set; }
        public int? scode { get; set; }
        public DateTime? billedtime { get; set; }
        public DateTime? collectedtime { get; set; }
        public bool? collectedstatus { get; set; }
        public bool? changedmanually { get; set; }
        public int? gcode { get; set; }
        public bool? receivedstatus { get; set; }
        public DateTime? receivedtime { get; set; }
        public bool? alldepartment { get; set; }
        public bool? is_emergency { get; set; }
        public string? tenant_code { get; set; }
    }


    // ══════════════════════════════════════════════════════════════
    // NEW: lab_request_specimentransfer — inter-department transfer
    // ══════════════════════════════════════════════════════════════
    [Table("lab_request_specimentransfer")]
    public class lab_request_specimentransfer
    {
        [ExplicitKey]
        public Guid lrsptid { get; set; }

        public Guid lrspid { get; set; }
        public Guid? requestguid { get; set; }
        public string? samplereference { get; set; }
        public int? defaults_code { get; set; }
        public int? scode { get; set; }
        public string? barcode { get; set; }

        public int from_gcode { get; set; }
        public int to_gcode { get; set; }

        public bool? transferstatus { get; set; }
        public DateTime? transferdatetime { get; set; }

        public bool? receivedstatus { get; set; }
        public DateTime? receiveddatetime { get; set; }

        public bool? completedstatus { get; set; }
        public DateTime? completeddatetime { get; set; }

        public bool? returnstatus { get; set; }
        public DateTime? returndatetime { get; set; }

        public bool? cancelledstatus { get; set; }
        public DateTime? cancelleddatetime { get; set; }

        public string? priority { get; set; } = "Normal";
        public string? transferreason { get; set; }
        public string? remarks { get; set; }

        public long? transferredby { get; set; }
        public long? receivedby { get; set; }
        public long? completedby { get; set; }

        public bool? is_emergency { get; set; }
        public bool? deleted { get; set; }
        public string? tenant_code { get; set; }

        public long? createdby { get; set; }
        public DateTime? createdon { get; set; }
        public long? modifiedby { get; set; }
        public DateTime? modifiedon { get; set; }
    }



    public class SaveSampleCollectionRequest
    {
        public lab_request_specimencollection collection { get; set; } = new();

        /// <summary>Written to samplelog only — never to specimencollection.</summary>
        public string? rejectreason { get; set; }

        /// <summary>Written to samplelog only — never to specimencollection.</summary>
        public string? resamplingreason { get; set; }
    }



    public class SaveSampleReceiveRequest
    {
        /// <summary>PK of the specimenreceive row to mark as received.</summary>
        public Guid lrsprid { get; set; }

        /// <summary>Injected by controller — do not send in body.</summary>
        public string? tenant_code { get; set; }
    }


    // ══════════════════════════════════════════════════════════════
    // NEW: request body for POST sampletransfer
    //
    //  action == TRANSFER (default) + no lrsptid  → creates a new
    //            transfer row from lrspid / from_gcode / to_gcode.
    //  action == RECEIVE / COMPLETE / RETURN / CANCEL + lrsptid
    //            → advances the state of an existing transfer row.
    // ══════════════════════════════════════════════════════════════
    public class SaveSampleTransferRequest
    {
        /// <summary>Null/empty = create a new transfer. Set = act on an existing transfer.</summary>
        public Guid? lrsptid { get; set; }

        /// <summary>Required when creating a new transfer (TRANSFER action).</summary>
        public Guid? lrspid { get; set; }

        public int? from_gcode { get; set; }
        public int? to_gcode { get; set; }

        public string? priority { get; set; }
        public string? transferreason { get; set; }
        public string? remarks { get; set; }
        public bool? is_emergency { get; set; }

        /// <summary>TRANSFER | RECEIVE | COMPLETE | RETURN | CANCEL — see SampleTransferAction.</summary>
        public string action { get; set; } = SampleTransferAction.Transfer;

        /// <summary>User performing the action — stamped as transferredby / receivedby / completedby.</summary>
        public long? usercode { get; set; }

        /// <summary>Injected by controller — do not send in body.</summary>
        public string? tenant_code { get; set; }
    }



    public class SpecimenWithLogDto
    {
        public lab_request_specimencollection Collection { get; set; } = new();
        public IList<lab_request_samplelog> Logs { get; set; } = new List<lab_request_samplelog>();
    }



    public class LoadSampleCollectionDto
    {
        public Guid? RequestGuid { get; set; }
        public int? RequestSno { get; set; }
        public string? RequestSnoprint { get; set; }
        public string? PatientName { get; set; }
        public string? Gender { get; set; }
        public string? AgeYears { get; set; }
        public DateTimeOffset? RequestDateTime { get; set; }
        public int? Bhcode { get; set; }
        public long? Scode { get; set; }
        public long? Gcode { get; set; }
        public string? SampleName { get; set; }
        public string? SampleShortname { get; set; }

        // Collection status
        public Guid? lrspId { get; set; }
        public string? Barcode { get; set; }
        public bool? CollectedStatus { get; set; }
        public DateTime? CollectedTime { get; set; }
        public bool? IsAccept { get; set; }
        public bool? IsReject { get; set; }
        public bool? IsResampling { get; set; }

        // Reasons from samplelog (latest entry)
        public string? RejectReason { get; set; }
        public string? ResamplingReason { get; set; }
        public bool? IsEmergency { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // RESPONSE DTO — loadsamplereceived
    // ═══════════════════════════════════════════════════════════
    public class LoadSampleReceivedDto
    {
        public Guid? LrspId { get; set; }
        public Guid? RequestGuid { get; set; }
        public string? SampleReference { get; set; }
        public string? Barcode { get; set; }
        public DateTime? CollectedTime { get; set; }
        public bool? IsAccept { get; set; }
        public DateTime? AcceptDateTime { get; set; }
        public int? Bhcode { get; set; }
        public bool? IsReject { get; set; }

        // Reason from samplelog
        public string? RejectReason { get; set; }

        public string? RequestSnoprint { get; set; }
        public string? PatientName { get; set; }
        public string? Gender { get; set; }
        public string? AgeYears { get; set; }
        public DateTimeOffset? RequestDateTime { get; set; }

        public long? Scode { get; set; }
        public long? Gcode { get; set; }
        public string? SampleName { get; set; }
        public string? SampleShortname { get; set; }

        public Guid? LrspRid { get; set; }
        public bool? ReceivedStatus { get; set; }
        public DateTime? ReceivedTime { get; set; }
        public string? GroupName { get; set; }
        public bool? IsEmergency { get; set; }
    }


    // ══════════════════════════════════════════════════════════════
    // NEW — RESPONSE DTOs for GET loadpatientstatus
    //
    //  One consolidated view per requestguid: for every test on the
    //  request, shows collection, every receive row (by gcode), every
    //  transfer row (by from/to gcode), and result entry / authorize1 /
    //  authorize2 status with the acting usercode.
    // ══════════════════════════════════════════════════════════════
    public class PatientStatusDto
    {
        public Guid? RequestGuid { get; set; }
        public string? RequestSnoprint { get; set; }
        public string? PatientName { get; set; }
        public string? Gender { get; set; }
        public string? AgeYears { get; set; }
        public DateTimeOffset? RequestDateTime { get; set; }
        public int? Bhcode { get; set; }

        public IList<TestStatusDto> Tests { get; set; } = new List<TestStatusDto>();
    }

    public class TestStatusDto
    {
        public int? Tcode { get; set; }
        public string? TestName { get; set; }
        public int? Scode { get; set; }
        public string? SampleName { get; set; }
        public int? Gcode { get; set; }
        public string? GroupName { get; set; }

        public CollectionStatusDetail? Collection { get; set; }
        public IList<ReceiveStatusDetail> Receives { get; set; } = new List<ReceiveStatusDetail>();
        public IList<TransferStatusDetail> Transfers { get; set; } = new List<TransferStatusDetail>();
        public ResultStatusDetail? Result { get; set; }
    }

    public class CollectionStatusDetail
    {
        public Guid? LrspId { get; set; }
        public string? Barcode { get; set; }
        public bool? CollectedStatus { get; set; }
        public DateTime? CollectedTime { get; set; }
        public bool? IsAccept { get; set; }
        public DateTime? AcceptDateTime { get; set; }
        public bool? IsReject { get; set; }
        public DateTime? RejectDateTime { get; set; }
        public string? RejectReason { get; set; }
        public bool? IsResampling { get; set; }
        public DateTime? ResamplingDateTime { get; set; }
        public string? ResamplingReason { get; set; }
    }

    public class ReceiveStatusDetail
    {
        public Guid? LrsprId { get; set; }
        public int? Gcode { get; set; }
        public string? GroupName { get; set; }
        public bool? ReceivedStatus { get; set; }
        public DateTime? ReceivedTime { get; set; }
    }

    public class TransferStatusDetail
    {
        public Guid? LrsptId { get; set; }

        public int? FromGcode { get; set; }
        public string? FromGroupName { get; set; }
        public int? ToGcode { get; set; }
        public string? ToGroupName { get; set; }

        public bool? TransferStatus { get; set; }
        public DateTime? TransferDateTime { get; set; }
        public long? TransferredBy { get; set; }

        public bool? ReceivedStatus { get; set; }
        public DateTime? ReceivedDateTime { get; set; }
        public long? ReceivedBy { get; set; }

        public bool? CompletedStatus { get; set; }
        public DateTime? CompletedDateTime { get; set; }
        public long? CompletedBy { get; set; }

        public bool? ReturnStatus { get; set; }
        public DateTime? ReturnDateTime { get; set; }

        public bool? CancelledStatus { get; set; }
        public DateTime? CancelledDateTime { get; set; }

        public string? Priority { get; set; }
        public string? TransferReason { get; set; }
        public string? Remarks { get; set; }
    }

    public class ResultStatusDetail
    {
        public bool? ResultStatus { get; set; }
        public int? ResultEnteredBy { get; set; }
        public DateTimeOffset? ResultEnteredDate { get; set; }

        public bool? IsAuthorized1 { get; set; }
        public int? ResultAuthorizedBy { get; set; }
        public DateTimeOffset? FirstAuthorizeDate { get; set; }

        public bool? IsAuthorized2 { get; set; }
        public int? ResultAuthorizedBy2 { get; set; }
        public DateTimeOffset? SecondAuthorizeDate { get; set; }
    }

    public class LoadSampleTransferDto
    {
        // Transfer identity
        public Guid? LrsptId { get; set; }
        public Guid? LrspId { get; set; }
        public Guid? RequestGuid { get; set; }
        public string? Barcode { get; set; }
        public int? Scode { get; set; }
        public string? SampleName { get; set; }
        public string? SampleShortname { get; set; }

        // Patient / request context
        public string? RequestSnoprint { get; set; }
        public string? PatientName { get; set; }
        public string? Gender { get; set; }
        public string? AgeYears { get; set; }
        public DateTimeOffset? RequestDateTime { get; set; }
        public int? Bhcode { get; set; }

        // Route
        public int FromGcode { get; set; }
        public string? FromGroupName { get; set; }
        public int ToGcode { get; set; }
        public string? ToGroupName { get; set; }

        // State machine
        public bool? TransferStatus { get; set; }
        public DateTime? TransferDateTime { get; set; }
        public long? TransferredBy { get; set; }

        public bool? ReceivedStatus { get; set; }
        public DateTime? ReceivedDateTime { get; set; }
        public long? ReceivedBy { get; set; }

        public bool? CompletedStatus { get; set; }
        public DateTime? CompletedDateTime { get; set; }
        public long? CompletedBy { get; set; }

        public bool? ReturnStatus { get; set; }
        public DateTime? ReturnDateTime { get; set; }

        public bool? CancelledStatus { get; set; }
        public DateTime? CancelledDateTime { get; set; }

        public string? Priority { get; set; }
        public string? TransferReason { get; set; }
        public string? Remarks { get; set; }
        public bool? IsEmergency { get; set; }
    }
}