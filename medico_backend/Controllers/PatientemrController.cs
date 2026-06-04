using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientEMRController : ControllerBase
    {
        private readonly PatientEMRClass cls;

        public PatientEMRController(PatientEMRClass _cls)
        {
            cls = _cls;
        }

        // ─────────────────────────────────────────────────────────
        // GET api/PatientEMR/problem-master
        // All active problem types for dropdown
        // ─────────────────────────────────────────────────────────
        [HttpGet("problem-master")]
        public async Task<IActionResult> GetProblemMaster()
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetProblemMasterList(tenant);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/PatientEMR/template?pbcode=5
        // Dynamic form template for a problem type.
        // Frontend calls this first to build the form,
        // then fills answers and POSTs the same structure to /save.
        //
        // Response shape per item:
        // {
        //   "prmid": "...",
        //   "slno": 1,
        //   "pbcode": 5,
        //   "resultvaluetype": "Paragraph",   ← Text|Paragraph|Selection|Multiple Selection|Heading
        //   "question": "Symptoms",
        //   "possibilities": [                ← only for Selection/Multiple Selection
        //     { "prbpid": "...", "possibility": "Fever", "sno": 1 }
        //   ]
        // }
        // ─────────────────────────────────────────────────────────
        [HttpGet("template")]
        public async Task<IActionResult> GetTemplate(int pbcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetEMRTemplate(pbcode, tenant);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // POST api/PatientEMR/save
        // Create or update a full EMR record.
        //
        // To CREATE (new):
        // {
        //   "opvisitid": "op_registration.op_id",
        //   "custid": 1001,
        //   "dcode": 12,
        //   "hdcode": 3,
        //   "notes": "Overall notes",
        //   "problemtype": "OPCASE",
        //   "problems":  [{ "pbcode": 5 }],
        //   "diagnoses": [{ "dccode": 10 }],
        //   "symptoms": [
        //     {
        //       "prmid": "...",
        //       "slno": 1, "pbcode": 5,
        //       "resultvaluetype": "Paragraph",
        //       "question": "Symptoms",
        //       "answer": "Fever, headache",
        //       "selected_possibilities": []
        //     },
        //     {
        //       "prmid": "...",
        //       "slno": 2, "pbcode": 5,
        //       "resultvaluetype": "Selection",
        //       "question": "Pain Location",
        //       "answer": "Head",
        //       "selected_possibilities": [
        //         { "prbpid": "...", "possibility": "Head", "isselected": true }
        //       ]
        //     }
        //   ]
        // }
        //
        // To UPDATE — include "problemid": "existing-id"
        // ─────────────────────────────────────────────────────────
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SaveEMRRequest req)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.SaveEMR(req, tenant);
            return Ok(res);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/PatientEMR/by-problem?problemid=...
        // Full EMR record — used when doctor opens a saved record
        // ─────────────────────────────────────────────────────────
        [HttpGet("by-problem")]
        public async Task<IActionResult> GetByProblem(string problemid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetEMRByProblemId(problemid, tenant);
            if (data == null) return NotFound("EMR record not found");
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/PatientEMR/by-visit?opvisitid=...
        // All EMR records for one OP visit
        // (one visit can have multiple — Vitals + Case Sheet etc.)
        // ─────────────────────────────────────────────────────────
        [HttpGet("by-visit")]
        public async Task<IActionResult> GetByVisit(string opvisitid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetEMRByVisit(opvisitid, tenant);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // GET api/PatientEMR/case-history?custid=1001
        // Flat history list for right panel — all visits for patient
        // ─────────────────────────────────────────────────────────
        [HttpGet("case-history")]
        public async Task<IActionResult> GetCaseHistory(decimal custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var data = await cls.GetCaseHistory(custid, tenant);
            return Ok(data);
        }

        // ─────────────────────────────────────────────────────────
        // DELETE api/PatientEMR/delete?problemid=...
        // Soft delete — sets deleted = true
        // ─────────────────────────────────────────────────────────
        [HttpDelete("delete")]
        public async Task<IActionResult> Delete(string problemid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.DeleteEMR(problemid, tenant);
            return Ok(res);
        }
    }
}