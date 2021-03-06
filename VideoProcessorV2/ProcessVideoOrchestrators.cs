using Microsoft.Azure.WebJobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VideoProcessor
{
    public static class ProcessVideoOrchestrators
    {
        [FunctionName("O_ProcessVideo")]
        public static async Task<object> ProcessVideo(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            log = ctx.CreateReplaySafeLogger(log);
            var videoLocation = ctx.GetInput<string>();

            log.LogInformation("About to call transcode video activity");
            
            string transcodedLocation = null;
            string thumbnailLocation = null;
            string withIntroLocation = null;
            string approvalResult = "Unknown";
            try
            {
                ctx.SetCustomStatus("transcoding");
                var transcodeResults =
                    await ctx.CallSubOrchestratorAsync<VideoFileInfo[]>("O_TranscodeVideo", videoLocation);

                transcodedLocation = transcodeResults
                        .OrderByDescending(r => r.BitRate)
                        .Select(r => r.Location)
                        .First();

                ctx.SetCustomStatus("extracting thumbnail");
                log.LogInformation("About to call extract thumbnail");

                thumbnailLocation = await
                    ctx.CallActivityAsync<string>("A_ExtractThumbnail", transcodedLocation);

                ctx.SetCustomStatus("prepending intro");
                log.LogInformation("About to call prepend intro");

                withIntroLocation = await
                    ctx.CallActivityAsync<string>("A_PrependIntro", transcodedLocation);

                ctx.SetCustomStatus("sending approval request email");
                await ctx.CallActivityAsync("A_SendApprovalRequestEmail", new ApprovalInfo()
                {
                    OrchestrationId = ctx.InstanceId,
                    VideoLocation = withIntroLocation
                });

                ctx.SetCustomStatus("waiting for email response");
                try
                {
                    approvalResult = await ctx.WaitForExternalEvent<string>("ApprovalResult", TimeSpan.FromSeconds(30));

                }
                catch (TimeoutException)
                {
                    log.LogWarning("Timed out waiting for approval");
                    approvalResult = "Timed Out";
                }

                if (approvalResult == "Approved")
                {
                    ctx.SetCustomStatus("publishing video");
                    await ctx.CallActivityAsync("A_PublishVideo", withIntroLocation);
                }
                else
                {
                    ctx.SetCustomStatus("rejecting video");
                    await ctx.CallActivityAsync("A_RejectVideo", withIntroLocation);
                }
                ctx.SetCustomStatus("finished");

            }
            catch (Exception e)
            {
                log.LogError($"Caught an error from an activity: {e.Message}");

                ctx.SetCustomStatus("error: cleaning up");
                await
                    ctx.CallActivityAsync<string>("A_Cleanup", 
                        new[] { transcodedLocation, thumbnailLocation, withIntroLocation });

                ctx.SetCustomStatus("finished with error");

                return new
                {
                    Error = "Failed to process uploaded video",
                    Message = e.Message
                };
            }

            return new
            {
                Transcoded = transcodedLocation,
                Thumbnail = thumbnailLocation,
                WithIntro = withIntroLocation,
                ApprovalResult = approvalResult
            };

        }

        [FunctionName("O_TranscodeVideo")]
        public static async Task<VideoFileInfo[]> TranscodeVideo(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            var videoLocation = ctx.GetInput<string>();
            var bitRates = await ctx.CallActivityAsync<int[]>("A_GetTranscodeBitrates", null);
            var transcodeTasks = new List<Task<VideoFileInfo>>();

            foreach (var bitRate in bitRates)
            {
                var info = new VideoFileInfo() { Location = videoLocation, BitRate = bitRate };
                var task = ctx.CallActivityAsync<VideoFileInfo>("A_TranscodeVideo", info);
                transcodeTasks.Add(task);
            }

            var transcodeResults = await Task.WhenAll(transcodeTasks);
            return transcodeResults;
        }

        [FunctionName("O_PeriodicTask")]
        public static async Task<int> PeriodicTask(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
            ILogger log)
        {
            log = ctx.CreateReplaySafeLogger(log);

            var timesRun = ctx.GetInput<int>();
            timesRun++;
            log.LogInformation($"Starting the PeriodicTask activity {ctx.InstanceId}, {timesRun}");
            await ctx.CallActivityAsync("A_PeriodicActivity", timesRun);
            var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
            await ctx.CreateTimer(nextRun, CancellationToken.None);
            ctx.ContinueAsNew(timesRun);
            return timesRun;
        }
    }
}
