using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Data;

namespace ERP_AGUS_01.Controllers
{
    public class StockController : Controller
    {
        private readonly DbHelper _db;

        public StockController(DbHelper db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            DataTable dt = _db.ExecuteQuery(@"
            select s.ItemId,i.ItemName,i.Unit,sum(s.Qty)Qty 
            from Stocks s
            join Items i on s.ItemId = i.ItemId
            group by s.ItemId,i.ItemName,i.Unit;
        ");

            return View(dt);
        }
    }
}
