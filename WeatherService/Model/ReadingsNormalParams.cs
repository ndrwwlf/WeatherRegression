using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherService.Model
{
    //public class ReadingsNormalParams
    //{
    //    public long AccID { get; set; }
    //    public long UtilID
    //    public long UnitID
    //    public 
    //}

    //{
    //    public WthNormalParams WthNormalParams { get; set; }
    //    public Readings Readings { get set; }
    //    public Accounts Accounts { get; set; }
    //    public 
    //}

    //public class WthNormalParams
    //{
    //    public int AccID { get; set; }
    //    public int UtilID { get; set; }
    //    public int B1 { get; set; }
    //    public int B2 { get; set; }
    //    public int B3 { get; set; }
    //    public int B4 { get; set; }
    //    public int B5 { get; set; }
    //}
}

//select w.AccID, w.UtilID, w.UnitID, w.B1, w.B2, w.B3, w.B4, w.B5,
//        r.RdngID, r.DateStart, r.DateEnd, r.Days, r.UnitID, r.Units, b.Zip,
//        l.LZip, u.CnvFct
//    from WthNormalParams w

//    join Accounts a on w.AccID = a.AccID

//    join Readings r on  a.AccID = r.AccID and r.DateStart >= '2016-12-01'

//    join Buildings b on a.BldID = b.BldID

//    join Locations l on b.LocID = l.LocID

//    join UnitTypes u on w.UnitID = u.UnitID

//    order by r.DateStart, w.AccID