using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace VideoProcessor
{
    /// <summary>
    /// This class contains all the HTTP endpoints
    /// </summary>
    public static class HttpFunctions
    {
        [FunctionName(nameof(ProcessVideoStarter))]
        public static async Task<IActionResult> ProcessVideoStarter(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // parse query parameter
            string video = req.GetQueryParameterDictionary()["video"];

            if (video == null)
            {
                return new BadRequestObjectResult(
                   "Please pass the video location the query string");
            }

            log.LogInformation($"About to start orchestration for {video}");

            var orchestrationId = await starter.StartNewAsync(nameof(OrchestratorFunctions.ProcessVideoOrchestrator), null, video);
            var payload = starter.CreateHttpManagementPayload(orchestrationId);
            return new OkObjectResult(payload);
        }

        [FunctionName(nameof(SubmitVideoApproval))]
        public static async Task<IActionResult> SubmitVideoApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SubmitVideoApproval/{id}")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            [Table("Approvals", "Approval", "{id}", Connection = "AzureWebJobsStorage")] Approval approval,
            ILogger log)
        {
            // nb if the approval code doesn't exist, framework just returns a 404 before we get here
            string result = req.GetQueryParameterDictionary()["result"];
            
            if (result == null)
                return new BadRequestObjectResult("Need an approval result");

            log.LogWarning($"Sending approval result to {approval.OrchestrationId} of {result}");
            // send the ApprovalResult external event to this orchestration
            await client.RaiseEventAsync(approval.OrchestrationId, "ApprovalResult", result);

            return new OkResult();
        }
        // use a fixed id, making it easier for us to terminate
        private const string PeriodicTaskInstanceId = "PeriodicTask"; 

        [FunctionName(nameof(StartPeriodicTask))]
        public static async Task<IActionResult> StartPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client, 
            ILogger log)
        {
            await client.StartNewAsync(nameof(OrchestratorFunctions.PeriodicTaskOrchestrator), PeriodicTaskInstanceId, 0);
            var payload = client.CreateHttpManagementPayload(PeriodicTaskInstanceId);
            return new OkObjectResult(payload);
        }

        [FunctionName(nameof(StopPeriodicTask))]
        public static async Task<IActionResult> StopPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var reason = "User requested termination";
            await client.TerminateAsync(PeriodicTaskInstanceId, reason);
            return new OkResult();
        }
    }
}
