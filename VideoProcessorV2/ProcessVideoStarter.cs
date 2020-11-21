using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace VideoProcessor
{
    public static class ProcessVideoStarter
    {
        [FunctionName("ProcessVideoStarter")]
        public static async Task<IActionResult> Run(
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

            var orchestrationId = await starter.StartNewAsync("O_ProcessVideo", video);
            var payload = starter.CreateHttpManagementPayload(orchestrationId);
            return new OkObjectResult(payload);
        }

        [FunctionName("SubmitVideoApproval")]
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

        [FunctionName("StartPeriodicTask")]
        public static async Task<IActionResult> StartPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client, 
            ILogger log)
        {
            var instanceId = "PeriodicTask"; // use a fixed id, making it easier for us to terminate
            await client.StartNewAsync("O_PeriodicTask", instanceId, 0);
            var payload = client.CreateHttpManagementPayload(instanceId);
            return new OkObjectResult(payload);
        }

        [FunctionName("StopPeriodicTask")]
        public static async Task<IActionResult> StopPeriodicTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = "PeriodicTask"; // use a fixed id, making it easier for us to terminate
            await client.TerminateAsync(instanceId, "User requested termination");
            return new OkResult();
        }
    }
}
