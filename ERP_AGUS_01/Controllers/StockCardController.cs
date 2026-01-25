using ERP_AGUS_01.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ERP_AGUS_01.Controllers
{
    public class StockCardController : Controller
    {
        private readonly DbHelper _db;

        public StockCardController(DbHelper db)
        {
            _db = db;
        }

        // INDEX
        public IActionResult Index(
            int? ItemId,
            int? WarehouseId,
            DateTime? DateFrom,
            DateTime? DateTo,
            string keyword
        )
        {
            var sql = @"
                SELECT
                    sc.StockCardId,
                    sc.TransDate,
                    i.ItemName,
                    w.WarehouseName,
                    l.LocationCode,
                    sc.TransType,
                    sc.QtyIn,
                    sc.QtyOut,
                    sc.ReferenceNo,
                    gr.ReceiptId
                FROM StockCards sc
                JOIN Items i ON sc.ItemId = i.ItemId
                JOIN Warehouses w ON sc.WarehouseId = w.WarehouseId
                LEFT JOIN WarehouseLocations l ON sc.LocationId = l.LocationId
                LEFT JOIN GoodsReceipts gr ON sc.ReferenceNo = gr.ReceiptNumber
                WHERE 1=1
            ";

            var param = new List<SqlParameter>();

            if (ItemId.HasValue)
            {
                sql += " AND sc.ItemId = @ItemId";
                param.Add(new SqlParameter("@ItemId", ItemId));
            }

            if (WarehouseId.HasValue)
            {
                sql += " AND sc.WarehouseId = @WarehouseId";
                param.Add(new SqlParameter("@WarehouseId", WarehouseId));
            }

            if (DateFrom.HasValue)
            {
                sql += " AND sc.TransDate >= @DateFrom";
                param.Add(new SqlParameter("@DateFrom", DateFrom));
            }

            if (DateTo.HasValue)
            {
                sql += " AND sc.TransDate <= @DateTo";
                param.Add(new SqlParameter("@DateTo", DateTo.Value.AddDays(1)));
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                sql += @" AND (
                    i.ItemName LIKE @kw
                    OR sc.ReferenceNo LIKE @kw
                    OR sc.TransType LIKE @kw
                )";
                param.Add(new SqlParameter("@kw", "%" + keyword + "%"));
            }

            sql += " ORDER BY sc.TransDate DESC";

            DataTable dt = _db.ExecuteQuery(sql, param.ToArray());

            ViewBag.Items = _db.ExecuteQuery("SELECT ItemId, ItemName FROM Items");
            ViewBag.Warehouses = _db.ExecuteQuery("SELECT WarehouseId, WarehouseName FROM Warehouses");

            return View(dt);
        }

        

    }
}
