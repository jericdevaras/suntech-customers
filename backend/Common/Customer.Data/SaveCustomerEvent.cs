using Newtonsoft.Json;

namespace Customer.Data
{
    public record SaveCustomerEvent
    {
        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int BirthdayInEpoch { get; set; }
        public string Email { get; set; } 
    }
}