### Durable Functions Video Processor Demo

This demo shows a very simple video processing workflow using the Azure Durable Functions extension and using version 2 of the Azure Functions runtime. It is based on the demo application in my [Durable Functions Fundamentals course on Pluralsight](https://pluralsight.pxf.io/c/1192349/424552/7490?u=www%2Epluralsight%2Ecom%2Fcourses%2Fazure-durable-functions-fundamentals)

To run this locally from Visual Studio you will need to create your own `local.settings.json` file with the following contents (filling in your personal email address and SendGrid key in order to be able to send emails):

```
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "TranscodeBitRates": "1010,2020,3030",
    "SendGridKey": "Your-SendGrid-Key-Here",
    "ApproverEmail": "your@email.here",
    "SenderEmail": "any@example.email",
    "Host": "http://localhost:7071",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet" 
  }
}
```

To run in the cloud, you will need to create a Function App, configure App Settings for each of the settings shown above, and push the code to it (you can use right-click publish in Visual Studio).

To start an orchestration, you can simply call the starter function, making sure to include a video query string parameteter. When running locally, this URL will be: http://localhost:7071/api/ProcessVideoStarter?video=example.mp4

If you are running in the cloud, the URL will look something like this: https://myfunctionapp.azurewebsites.net/api/ProcessVideoStarter?code=yoursecretfunctioncode&video=example.mp4 You can find your secret function authorization code by clicking the get URL link for your function in the portal.

There is also a periodic task you can trigger with the `StartPeriodicTask` endpoint, but it will run forever, so remember to terminate the orchestration, or delete the function app once you're done.

