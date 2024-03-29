﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;

namespace VideoProcessor;

public static class ProcessVideoActivities
{
    [FunctionName("A_GetTranscodeBitrates")]
    public static int[] GetTranscodeBitrates(
                        [ActivityTrigger] object input)
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
        ILogger log)
    {
        log.LogInformation($"Transcoding {inputVideo.Location} to {inputVideo.BitRate}");
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
        ILogger log)
    {
        log.LogInformation($"Extracting Thumbnail {inputVideo}");

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
        ILogger log)
    {
        log.LogInformation($"Appending intro to video {inputVideo}");
        var introLocation = Environment.GetEnvironmentVariable("IntroLocation");
        // simulate doing the activity
        await Task.Delay(5000);

        return "withIntro.mp4";
    }

    [FunctionName("A_Cleanup")]
    public static async Task<string> Cleanup(
        [ActivityTrigger] string[] filesToCleanUp,
        ILogger log)
    {
        foreach (var file in filesToCleanUp.Where(f => f != null))
        {
            log.LogInformation($"Deleting {file}");
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
        ILogger log)
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
        
        log.LogInformation($"Sending approval request for {approvalInfo.VideoLocation}");
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
        log.LogWarning(body);
    }

    [FunctionName("A_PublishVideo")]
    public static async Task PublishVideo(
        [ActivityTrigger] string inputVideo,
        ILogger log)
    {
        log.LogInformation($"Publishing {inputVideo}");
        // simulate publishing
        await Task.Delay(1000);
    }

    [FunctionName("A_RejectVideo")]
    public static async Task RejectVideo(
        [ActivityTrigger] string inputVideo,
        ILogger log)
    {
        log.LogInformation($"Rejecting {inputVideo}");
        // simulate performing reject actions
        await Task.Delay(1000);
    }

    [FunctionName("A_PeriodicActivity")]
    public static void PeriodicActivity(
        [ActivityTrigger] int timesRun,
        ILogger log)
    {
        log.LogWarning($"Running the periodic activity, times run = {timesRun}");
    }
}
