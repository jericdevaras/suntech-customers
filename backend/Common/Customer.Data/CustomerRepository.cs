using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Customer.Data
{
    public class CustomerRepository
    {
        private Container _container;

        public CustomerRepository(Database database) 
        {
            _container = database.GetContainer(id: Environment.GetEnvironmentVariable("CustomerOverviewContainer"));
        }

        public async Task<CustomerOverview> GetCustomer(string email)
        {
            CustomerOverview? result = null;

            var queryable = _container.GetItemLinqQueryable<CustomerOverview>();
                    
            var matches = queryable.Where(e => e.Email == email);

            using FeedIterator<CustomerOverview> linqFeed = matches.ToFeedIterator();

            while (linqFeed.HasMoreResults)
            {
                FeedResponse<CustomerOverview> response = await linqFeed.ReadNextAsync();

                foreach (CustomerOverview item in response)
                {
                    result = item;
                    break;
                }
            }
           
            return result;
        }

        public async Task CreateCustomer(CustomerOverview customerOverview)
        {
            var partitionKey = new PartitionKey(customerOverview.Email);

            await _container.CreateItemAsync(customerOverview, partitionKey);
        }

        public async Task UpdateCustomer(CustomerOverview customerOverview)
        {
            var partitionKey = new PartitionKey(customerOverview.Email);

            await _container.ReplaceItemAsync(customerOverview, customerOverview.Id.ToString(), partitionKey);
        }
    }

}
