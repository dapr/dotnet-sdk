using System.ComponentModel.DataAnnotations;

namespace ControllerSample
{
    public class Transaction
    {
        [Required]
        public string Id { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }
    }
}