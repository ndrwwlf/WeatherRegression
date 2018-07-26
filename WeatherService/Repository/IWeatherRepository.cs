using System;
using System.Collections.Generic;
using WeatherService.Dao;
using WeatherService.Db;
using WeatherService.Dto;
using WeatherService.Model;

namespace WeatherService.Services
{
    public interface IWeatherRepository
    {
        List<string> GetDistinctZipCodes();
        bool InsertWeatherData(WeatherData weatherData);
        bool GetWeatherDataExistForZipAndDate(string ZipCode, DateTime rDate);
        IDictionary<string, IEnumerable<string>> GetAllWeatherData();
        List<WeatherData> GetWeatherData(PageParams pageParams);
        List<WeatherData> GetWeatherDataByZipCode(string ZipCode, PageParams pageParams);
        int GetWeatherDataRowCount();
        int GetWeatherDataRowCountByZip(string ZipCode);
        List<ReadingsQueryResult> GetReadings(string DateStart);
        int GetExpectedWthExpUsageRowCount(string DateStart);
        int GetActualWthExpUsageRowCount();
        List<WeatherData> GetWeatherDataByZipStartAndEndDate(string ZipCode, DateTime DateStart, DateTime DateEnd);
        bool InsertWthExpUsage(int readingId, decimal value, int AccID, int UtilID, int UnitID);

        List<WthNormalParams> GetNormalParamsKeysForRegression();
        List<ReadingsQueryResult> GetReadingsForRegressionYear(string DateTimeStart, WthNormalParams accountAndUtil);
        bool InsertWthNormalParams(WthNormalParams normalParams, bool Accord);
        List<ReadingsQueryResult> GetReadingsFromExpUsageOriginal();
        List<ReadingsQueryResult> GetReadingsFromExpUsageOriginalCorrected();
        List<ReadingsQueryResult> GetReadingsFromExpUsageOriginalCorrected(AccordResult accord);
        WthNormalParams GetParamsForReading(int AccID, int UtilID, int UnitID);
        bool InsertMyWthExpUsage(WthExpUsage wthExpUsage);
        bool InsertMyWthExpUsage(WthExpUsage wthExpUsage, bool Accord);
    }
}
