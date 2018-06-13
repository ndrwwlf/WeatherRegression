using System.ComponentModel.DataAnnotations;

namespace WeatherService.Dto
{
    public class PageParams
    {
        [Required, Range(1, int.MaxValue, ErrorMessage = "Please enter a positive page number")]
        public int PageNumber { get; set; }

        [Required, Range(1, int.MaxValue)]
        public int RowsPerPage { get; set; }
    }
}
