using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherService.Db
{
    [Table("Location")]
    public class Location
    {
        public int Id { get; set; }

        [Required, StringLength(10)]
        public string ZipCode { get; set; }

        [Required, StringLength(10)]
        public string StationId { get; set; }

    }
}
