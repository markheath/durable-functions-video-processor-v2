using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using SendGrid.Helpers.Mail;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace VideoProcessor
{
    public static class ProcessVideoActivities
    {
        [FunctionName("A_GetTranscodeBitrates")]
        public static int[] GetTranscodeBitrates(
                            [ActivityTrigger] object input,
                            TraceWriter log)
        {
            var bitRates = Environment.GetEnvironmentVariable("TranscodeBitrates");
            return bitRates
                        .Split(',')
                        .Select(int.Parse)
                        .ToArray();
        }

        [FunctionName("A_TranscodeVideo")]
        public static async Task<VideoFileInfo> TranscodeVideo(
            [ActivityTrigger] VideoFileInfo inputVideo,
            TraceWriter log)
        {
            log.Info($"Transcoding {inputVideo.Location} to {inputVideo.BitRate}");
            // simulate doing the activity
            await Task.Delay(5000);

            var transcodedLocation = $"{Path.GetFileNameWithoutExtension(inputVideo.Location)}-" +
                $"{inputVideo.BitRate}kbps.mp4";

            return new VideoFileInfo
            {
                Location = transcodedLocation,
                BitRate = inputVideo.BitRate
            };
        }

        [FunctionName("A_ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
            [ActivityTrigger] string inputVideo,
            TraceWriter log)
        {
            log.Info($"Extracting Thumbnail {inputVideo}");

            if (inputVideo.Contains("error"))
            {
                throw new InvalidOperationException("Could not extract thumbnail");
            }

            // simulate doing the activity
            await Task.Delay(5000);

            return "thumbnail.png";
        }

        [FunctionName("A_PrependIntro")]
        public static async Task<string> PrependIntro(
            [ActivityTrigger] string inputVideo,
            TraceWriter log)
        {
            log.Info($"Appending intro to video {inputVideo}");
            var introLocation = Environment.GetEnvironmentVariable("IntroLocation");
            // simulate doing the activity
            await Task.Delay(5000);

            return "withIntro.mp4";
        }

        [FunctionName("A_Cleanup")]
        public static async Task<string> Cleanup(
            [ActivityTrigger] string[] filesToCleanUp,
            TraceWriter log)
        {
            foreach (var file in filesToCleanUp.Where(f => f != null))
            {
                log.Info($"Deleting {file}");
                // simulate doing the activity
                await Task.Delay(1000);
            }
            return "Cleaned up successfully";
        }

        [FunctionName("A_SendApprovalRequestEmail")]
        public static void SendApprovalRequestEmail(
            [ActivityTrigger] ApprovalInfo approvalInfo,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
            [Table("Approvals", "AzureWebJobsStorage")] out Approval approval,
            TraceWriter log)
        {
            var approvalCode = Guid.NewGuid().ToString("N");
            approval = new Approval
            {
                PartitionKey = "Approval",
                RowKey = approvalCode,
                OrchestrationId = approvalInfo.OrchestrationId
            };
            var approverEmail = new EmailAddress(Environment.GetEnvironmentVariable("ApproverEmail"));
            var senderEmail = new EmailAddress(Environment.GetEnvironmentVariable("SenderEmail"));
            
            log.Info($"Sending approval request for {approvalInfo.VideoLocation}");
            var host = Environment.GetEnvironmentVariable("Host");

            var functionAddress = $"{host}/api/SubmitVideoApproval/{approvalCode}";
            var approvedLink = functionAddress + "?result=Approved";
            var rejectedLink = functionAddress + "?result=Rejected";
            var body = $"Please review {approvalInfo.VideoLocation}<br>"
                               + $"<a href=\"{approvedLink}\">Approve</a><br>"
                               + $"<a href=\"{rejectedLink}\">Reject</a>";
            message = new SendGridMessage();
            message.Subject = "A video is awaiting approval (V2)";
            message.From = senderEmail;
            message.AddTo(approverEmail);
            message.HtmlContent = body;
            log.Warning(body);
        }

        [FunctionName("A_PublishVideo")]
        public static async Task PublishVideo(
            [ActivityTrigger] string inputVideo,
            TraceWriter log)
        {
            log.Info($"Publishing {inputVideo}");
            // simulate publishing
            await Task.Delay(1000);
        }

        [FunctionName("A_RejectVideo")]
        public static async Task RejectVideo(
            [ActivityTrigger] string inputVideo,
            TraceWriter log)
        {
            log.Info($"Rejecting {inputVideo}");
            // simulate performing reject actions
            await Task.Delay(1000);
        }

        [FunctionName("A_PeriodicActivity")]
        public static void PeriodicActivity(
            [ActivityTrigger] int timesRun,
            TraceWriter log)
        {
            log.Warning($"Running the periodic activity, times run = {timesRun}");
        }
    }
}
