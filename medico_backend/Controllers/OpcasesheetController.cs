using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    // ─────────────────────────────────────────────────────────────
    // NEW OP CASE SHEET CONTROLLER
    // Uses new normalized tables:
    //   op_case_sheet, op_case_sheet_diagnosis,
    //   op_case_sheet_symptoms, op_prescription_master,
    //   op_prescription_detail, op_investigation_master,
    //   op_investigation_detail
    //
    // NOTE: Vitals are NOT handled here.
    //   Use existing endpoints:
    //     POST api/OpRegistration/save-vitals
    //     POST api/OpRegistration/update-vitals
    //     GET  api/OpRegistration/vitals/all
    //     GET  api/OpRegistration/vitals/detail
    // ─────────────────────────────────────────────────────────────
    [Route("api/[controller]")]
    [ApiController]
    public class CaseSheetController : ControllerBase
    {
        private readonly NewOPCaseSheetClass cls;
        private readonly HmsBillingClass billingCls;

        public CaseSheetController(NewOPCaseSheetClass _cls, HmsBillingClass _billingCls)
        {
            cls = _cls;
            billingCls = _billingCls;
        }

        // ─────────────────────────────────────────────────────────
        // POST api/CaseSheet/save
        //
        // Save the full OP Case Sheet in one call.
        // Handles: chief complaint + symptoms + examination +
        //          diagnosis + advise + prescription + investigation.
        //
        // CREATE (new sheet):
        // {
        //   "op_id": "<uuid>",
        //   "custid": 1001,
        //   "dcode": 12,
        //   "sheet_status": "DRAFT",
        //   "chief_complaint": "Fever for 3 days",
        //   "symptoms": "Fever, headache",
        //   "examination": "Throat congested",
        //   "diagnosis": "Viral fever",
        //   "advise": "Rest and fluids",
        //   "followup_date": "2026-06-10",
        //   "symptom_list": [
        //     { "sno": 1, "symptom_text": "Fever", "duration": "3 days", "severity": "MODERATE" },
        //     { "sno": 2, "symptom_text": "Headache", "duration": "2 days", "severity": "MILD" }
        //   ],
        //   "diagnosis_list": [
        //     {
        //       "sno": 1,
        //       "icd_code": "J06.9",
        //       "icd_description": "Acute upper respiratory infection",
        //       "diagnosis_text": "Viral fever",
        //       "diagnosis_type": "PRIMARY",
        //       "condition_type": "ACUTE",
        //       "severity": "MODERATE",
        //       "status": "ACTIVE"
        //     }
        //   ],
        //   "prescription": {
        //     "topremarks": "Take after food",
        //     "bottonremarks": "Review after 5 days",
        //     "items": [
        //       {
        //         "sno": 1,
        //         "drug_name": "Paracetamol 500mg",
        //         "morning": "1", "afternoon": "1", "evening": "0", "night": "1",
        //         "before_food": false, "after_food": true,
        //         "days": 5, "qty": 15, "route": "ORAL"
        //       }
        //     ]
        //   },
        //   "investigation": {
        //     "is_urgent": false,
        //     "notes": "Fasting required for blood sugar",
        //     "tests": [
        //       { "sno": 1, "test_name": "CBC", "test_code": 10, "test_category": "Haematology" },
        //       { "sno": 2, "test_name": "Blood Sugar Fasting", "test_category": "Biochemistry" }
        //     ]
        //   }
        // }
        //
        // UPDATE — include "sheet_id": "<existing-uuid>"
        //   and/or "prescription": { "pr_code": "PR/2026/06/0001", ... }
        //   and/or "investigation": { "inv_id": "<uuid>", ... }
        // ─────────────────────────────────────────────────────────
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SaveCaseSheetRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");

            if (string.IsNullOrWhiteSpace(req.op_id))
                return BadRequest("op_id is required");

            var res = await cls.SaveCaseSheet(req, tenant);

            if (res.StartsWith("OP Registration not found") ||
                res.StartsWith("Case sheet not found"))
                return NotFound(res);

            return Ok(res);
        }

        // ─────────────────────────────────────────────────────────
        // POST api/CaseSheet/finalize
        //
        // Mark a DRAFT sheet as FINAL after consultation.
        // {
        //   "sheet_id": "<uuid>",
        //   "is_consulted": true
        // }
        // ─────────────────────────────────────────────────────────
        [HttpPost("finalize")]
        public async Task<IActionResult> Finalize([FromBody] FinalizeCaseSheetRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            if (string.IsNullOrWhiteSpace(tenant))
                return BadRequest("tenant_code header is required");

            if (string.IsNullOrWhiteSpace(req.sheet_id))
                return BadRequest("sheet_id is required");

            var res = await cls.FinalizeCaseSheet(req, tenant);
            return Ok(res);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/CaseSheet/by-visit?op_id=...
        //
        // Returns the full case sheet for a visit:
        // {
        //   "sheet_id": "...",
        //   "op_id": "...",
        //   "custid": 1001,
        //   "dcode": 12,
        //   "visit_date": "...",
        //   "chief_complaint": "...",
        //   "symptoms": "...",
        //   "examination": "...",
        //   "diagnosis": "...",
        //   "advise": "...",
        //   "sheet_status": "DRAFT",
        //   "symptom_list": [...],
        //   "diagnosis_list": [...],
        //   "prescription": { "pr_code": "...", "items": [...] },
        //   "investigation": { "inv_code": "...", "tests": [...] }
        // }
        // ─────────────────────────────────────────────────────────
        [HttpGet("by-visit")]
        public async Task<IActionResult> GetByVisit([FromQuery] string op_id)
        {
            if (string.IsNullOrWhiteSpace(op_id))
                return BadRequest("op_id is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetCaseSheetByVisit(op_id, tenant);

            if (data == null)
                return NotFound("No case sheet found for this visit");

            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/CaseSheet/prescription?op_id=...
        // Returns only the prescription for a visit
        // ─────────────────────────────────────────────────────────
        [HttpGet("prescription")]
        public async Task<IActionResult> GetPrescription([FromQuery] string op_id)
        {
            if (string.IsNullOrWhiteSpace(op_id))
                return BadRequest("op_id is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetPrescription(op_id, tenant);

            if (data == null)
                return NotFound("No prescription found for this visit");

            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/CaseSheet/investigation?op_id=...
        // Returns only the investigation order for a visit
        // ─────────────────────────────────────────────────────────
        [HttpGet("investigation")]
        public async Task<IActionResult> GetInvestigation([FromQuery] string op_id)
        {
            if (string.IsNullOrWhiteSpace(op_id))
                return BadRequest("op_id is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetInvestigation(op_id, tenant);

            if (data == null)
                return NotFound("No investigation found for this visit");

            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/CaseSheet/history?custid=...&pageNo=1&pageSize=10
        // Returns all past case sheets for a patient (for EMR view)
        // ─────────────────────────────────────────────────────────
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] decimal custid,
            [FromQuery] int pageNo = 1,
            [FromQuery] int pageSize = 10)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetPatientHistory(custid, tenant, pageSize, pageNo);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/CaseSheet/icd-search?query=fever&limit=20
        // Search ICD-10 codes for diagnosis autocomplete
        // ─────────────────────────────────────────────────────────
        [HttpGet("icd-search")]
        public async Task<IActionResult> IcdSearch(
            [FromQuery] string query,
            [FromQuery] int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return BadRequest("Query must be at least 2 characters");

            var data = await cls.SearchIcd(query, limit);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // PUT api/CaseSheet/investigation/result
        //
        // Update a lab/investigation test result.
        // {
        //   "inv_det_id": "<uuid>",
        //   "result_value": "13.5 g/dL",
        //   "result_notes": "Within normal range",
        //   "result_status": "COMPLETED"
        // }
        // ─────────────────────────────────────────────────────────
        [HttpPost("investigation/result")]
        public async Task<IActionResult> UpdateResult(
            [FromBody] UpdateInvestigationResultRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            if (string.IsNullOrWhiteSpace(req.inv_det_id))
                return BadRequest("inv_det_id is required");

            var res = await cls.UpdateInvestigationResult(req, tenant);
            return Ok(res);
        }

        // ─────────────────────────────────────────────────────────
        // DELETE api/CaseSheet/prescription/delete?pr_code=...
        // Soft delete a prescription
        // ─────────────────────────────────────────────────────────
        [HttpGet("prescription/delete")]
        public async Task<IActionResult> DeletePrescription([FromQuery] string pr_code)
        {
            if (string.IsNullOrWhiteSpace(pr_code))
                return BadRequest("pr_code is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.DeletePrescription(pr_code, tenant);
            return Ok(res);
        }

        // ─────────────────────────────────────────────────────────
        // DELETE api/CaseSheet/investigation/delete?inv_id=...
        // Soft delete an investigation order
        // ─────────────────────────────────────────────────────────
        [HttpGet("investigation/delete")]
        public async Task<IActionResult> DeleteInvestigation([FromQuery] string inv_id)
        {
            if (string.IsNullOrWhiteSpace(inv_id))
                return BadRequest("inv_id is required");

            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.DeleteInvestigation(inv_id, tenant);
            return Ok(res);
        }
        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            var result = await cls.GetAllIcd();

            if (result == null || result.Count == 0)
                return NotFound("No ICD records found");

            return Ok(result);
        }

    }
}