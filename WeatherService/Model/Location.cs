using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherService.Db
{
    [Table("Location")]
    public class Location
    {
        public int ID { get; set; }
        [Required, StringLength(10)]
        public string ZipCode { get; set; }
    }
}
