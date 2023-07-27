using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Data = Customer.Data;
using Microsoft.Azure.Cosmos;
using Customer.Data;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Database = Microsoft.Azure.Cosmos.Database;

namespace Customer
{
    public static class Customer
    {
        private static readonly CosmosClient Client =
            new CosmosClient(Environment.GetEnvironmentVariable("CustomerDbConnection"),
            new CosmosClientOptions()
            {
                SerializerOptions = new CosmosSerializationOptions()
                {
                    IgnoreNullValues = true
                }
            });

        private static readonly Database Database = Client.GetDatabase(Environment.GetEnvironmentVariable("CustomerDatabase"));

        [FunctionName("SaveCustomer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var eventId = Guid.NewGuid();

            log.LogInformation($"SaveCustomer processed a request- {eventId}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var saveCustomerEvent = JsonConvert.DeserializeObject<SaveCustomerEvent>(requestBody);
            saveCustomerEvent.Id = eventId;

            if (string.IsNullOrEmpty(saveCustomerEvent.Email))
            {
                return new BadRequestObjectResult("Invalid Email for customer.");
            }

            try
            {
                var eventsContainer = Database.GetContainer(id: Environment.GetEnvironmentVariable("CustomerEventsContainer"));
                await eventsContainer.CreateItemAsync(saveCustomerEvent);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Saving customer event failed. {eventId}-{saveCustomerEvent.Email}");
                throw;
            }

            string responseMessage = $"{saveCustomerEvent.Email} customer saved.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("CustomerChanges")]
        public static async Task RunAsync([CosmosDBTrigger(
            databaseName: "%CustomerDatabase%",
            collectionName: "%CustomerEventsContainer%",
            ConnectionStringSetting = "CustomerDbConnection",
            LeaseCollectionName = "leases")]IReadOnlyList<Document> input,
           [EventGrid(TopicEndpointUri = "EventGridEndpoint", TopicKeySetting = "EventGridKey")] IAsyncCollector<EventGridEvent> eventGridOutput,
           ILogger log)
        {

            var customerRepository = new CustomerRepository(Database);

            if (input != null && input.Count > 0)
            {
                log.LogInformation($"Customer documents modified {input.Count}");
              
                foreach (var inputItem in input)
                {
                    var customerEvent = JsonConvert.DeserializeObject<SaveCustomerEvent>(inputItem.ToString());
                    var materializedEvent = await MaterializeCustomer(customerRepository, customerEvent, log);
                    var eventGridEvent = new EventGridEvent(materializedEvent.Email, "Customer.Data.CustomerOverview", "1.0", materializedEvent);
                    await eventGridOutput.AddAsync(eventGridEvent);
                }
            }
        }

        private static async Task<CustomerOverview> MaterializeCustomer(CustomerRepository customerRepository, SaveCustomerEvent saveCustomerEvent, ILogger log)
        {
            var customer = new CustomerOverview()
            {
                Id = Guid.NewGuid(),
                Email = saveCustomerEvent.Email,
                FirstName = saveCustomerEvent.FirstName,
                LastName = saveCustomerEvent.LastName,
                BirthdayInEpoch = saveCustomerEvent.BirthdayInEpoch,
                LastUpdated = DateTime.UtcNow.ToString("D")
            };

            var currentCustomer = await customerRepository.GetCustomer(saveCustomerEvent.Email);

            if (currentCustomer != null)
            {
                customer.Id = currentCustomer.Id;
                await customerRepository.UpdateCustomer(customer);
                log.LogInformation($"Customer overview {customer.Id} modified.");
            }
            else
            {
                await customerRepository.CreateCustomer(customer);
                log.LogInformation($"Customer overview {customer.Id} created.");
            }

            return customer;
        }
    }
}
