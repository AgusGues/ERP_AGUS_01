using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using ERP_AGUS_01.Data;
using ERP_AGUS_01.Models;

namespace ERP_AGUS_01.Controllers
{
    public class StockTransferController : Controller
    {
        private readonly DbHelper _db;
        public StockTransferController(DbHelper db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var dt = _db.ExecuteQuery(
                @"
                SELECT 
                t.TransferId,
                t.TransferNumber,
                t.TransferDate,
                i.ItemName,
                wFrom.WarehouseName AS FromWarehouse,
                wTo.WarehouseName   AS ToWarehouse,
                lFrom.LocationCode AS FromLocation,
                lTo.LocationCode   AS ToLocation,
                sd.Qty,
                t.Status
            FROM StockTransfers t
            JOIN Warehouses wFrom 
                ON t.FromWarehouseId = wFrom.WarehouseId
            JOIN Warehouses wTo
                ON t.ToWarehouseId = wTo.WarehouseId
            JOIN WarehouseLocations lFrom
                ON t.FromLocationId = lFrom.LocationId
            JOIN WarehouseLocations lTo
                ON t.ToLocationId = lTo.LocationId
            JOIN StockTransferDetails sd on t.TransferId = sd.TransferId
            JOIN Items i on sd.ItemId = i.ItemId
            ORDER BY t.TransferDate DESC;
            ");

            ViewBag.Warehouses = _db.ExecuteQuery("SELECT * FROM Warehouses");
            ViewBag.Locations = _db.ExecuteQuery("SELECT * FROM WarehouseLocations");
            

            return View(dt);
        }

        public IActionResult SearchItem(string term)
        {
            DataTable dt = _db.ExecuteQuery(@"
                                            SELECT TOP 10 ItemId, ItemName
                                            FROM Items
                                            WHERE ItemName LIKE '%' + @term + '%'
                                            ORDER BY ItemName",
                new[] {
                        new SqlParameter("@term", term)
                });

            var data = dt.AsEnumerable().Select(r => new
            {
                label = r["ItemName"].ToString(), // yang tampil
                value = r["ItemId"].ToString()    // yang disimpan
            });

            return Json(data);
        }

        [HttpPost]
        public IActionResult SaveDraft(StockTransferVM model)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {

                string transferNo = "TRF-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                //1. StockTransfers HEADER
                int transferId = Convert.ToInt32(_db.ExecuteScalar(@"
                        insert into StockTransfers(
                        TransferNumber,
                        TransferDate,
                        FromWarehouseId,
                        ToWarehouseId,
                        FromLocationId,
                        ToLocationId,
                        Status
                        )
                        VALUES
                        (
                        @TransferNumber,
                        GETDATE(),
                        @FromWarehouseId,
                        @ToWarehouseId,
                        @FromLocationId,
                        @ToLocationId,
                        'DRAFT');
                    SELECT SCOPE_IDENTITY();",
                    new[]
                    {
                        new SqlParameter("@TransferNumber",transferNo),
                        new SqlParameter("@FromWarehouseId",model.FromWarehouseId),
                        new SqlParameter("@ToWarehouseId",model.ToWarehouseId),
                        new SqlParameter("@FromLocationId",model.FromLocationId),
                        new SqlParameter("@ToLocationId",model.ToLocationId)
                    },
                    conn, tran)
                    );

                //2. Insert StockTransferDetails
                foreach (var item in model.Items)
                {
                    _db.ExecuteNonQuery(@"
                        INSERT INTO StockTransferDetails
                        (
                            TransferId,
                            ItemId,
                            Qty
                        )
                        VALUES
                        (
                            @TransferId,
                            @ItemId,
                            @Qty
                        )",
                new[]
                {
                    new SqlParameter("@TransferId", transferId),
                    new SqlParameter("@ItemId", item.ItemId),
                    new SqlParameter("@Qty", item.Qty)
                },
                conn, tran);
                }

                tran.Commit();
                TempData["Success"] = "Draft Transfer Stock Berhasil di Simpan";
            }
            catch 
            {
                tran.Rollback();
                throw;
            }

            return RedirectToAction("Index");
        }

        public IActionResult PostTransfer(int id)
        {
            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // 1️⃣ HEADER TRANSFER
                DataTable header = _db.ExecuteQuery(@"
                SELECT *
                FROM StockTransfers
                WHERE TransferId = @id
                AND Status = 'DRAFT'",
                    new[] { new SqlParameter("@id", id) },
                    conn, tran);

                if (header.Rows.Count == 0)
                    throw new Exception("Transfer tidak valid atau sudah diposting");

                DataRow h = header.Rows[0];

                // 2️⃣ DETAIL
                DataTable details = _db.ExecuteQuery(@"
                    SELECT *
                    FROM StockTransferDetails
                    WHERE TransferId = @id",
                    new[] { new SqlParameter("@id", id) },
                    conn, tran);

                foreach (DataRow d in details.Rows)
                {
                    int itemId = Convert.ToInt32(d["ItemId"]);
                    decimal qty = Convert.ToDecimal(d["Qty"]);

                    int fromWh = Convert.ToInt32(h["FromWarehouseId"]);
                    int fromLoc = Convert.ToInt32(h["FromLocationId"]);
                    int toWh = Convert.ToInt32(h["ToWarehouseId"]);
                    int toLoc = Convert.ToInt32(h["ToLocationId"]);

                    // 3️⃣ CEK STOCK ASAL
                    decimal currentStock = Convert.ToDecimal(
                        _db.ExecuteScalar(@"
                        SELECT ISNULL(Qty,0)
                        FROM Stocks
                        WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                            new[] {
                            new SqlParameter("@item", itemId),
                            new SqlParameter("@wh", fromWh),
                            new SqlParameter("@loc", fromLoc)
                            },
                            conn, tran)
                    );

                    if (currentStock < qty)
                        throw new Exception($"🚨 Stock tidak mencukupi (ItemId {itemId})");

                    // =========================
                    // 4️⃣ STOCK KELUAR
                    // =========================

                    // UPDATE STOCK
                    _db.ExecuteNonQuery(@"
                        UPDATE Stocks
                        SET Qty = Qty - @qty
                        WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                        new[] {
                        new SqlParameter("@qty", qty),
                        new SqlParameter("@item", itemId),
                        new SqlParameter("@wh", fromWh),
                        new SqlParameter("@loc", fromLoc)
                        },
                        conn, tran);

                    decimal balanceOut = currentStock - qty;

                    // STOCK CARD OUT
                    _db.ExecuteNonQuery(@"
                        INSERT INTO StockCards
                        (TransDate, ItemId, WarehouseId, LocationId, TransType, ReferenceNo, QtyIn, QtyOut, Balance)
                        VALUES
                        (GETDATE(), @item, @wh, @loc, 'TRANSFER_OUT', @ref, 0, @qty, @balance)",
                        new[] {
                        new SqlParameter("@item", itemId),
                        new SqlParameter("@wh", fromWh),
                        new SqlParameter("@loc", fromLoc),
                        new SqlParameter("@qty", qty),
                        new SqlParameter("@balance", balanceOut),
                        new SqlParameter("@ref", h["TransferNumber"])
                        },
                        conn, tran);

                    // =========================
                    // 5️⃣ STOCK MASUK
                    // =========================

                    decimal destStock = Convert.ToDecimal(
                        _db.ExecuteScalar(@"
                        SELECT ISNULL(Qty,0)
                        FROM Stocks
                        WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                            new[] {
                            new SqlParameter("@item", itemId),
                            new SqlParameter("@wh", toWh),
                            new SqlParameter("@loc", toLoc)
                            },
                            conn, tran)
                    );

                    int exists = Convert.ToInt32(
                        _db.ExecuteScalar(@"
                            SELECT COUNT(*)
                            FROM Stocks
                            WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                            new[] {
                            new SqlParameter("@item", itemId),
                            new SqlParameter("@wh", toWh),
                            new SqlParameter("@loc", toLoc)
                            },
                            conn, tran)
                    );

                    if (exists == 0)
                    {
                        _db.ExecuteNonQuery(@"
                            INSERT INTO Stocks (ItemId, WarehouseId, LocationId, Qty)
                            VALUES (@item, @wh, @loc, @qty)",
                            new[] {
                            new SqlParameter("@item", itemId),
                            new SqlParameter("@wh", toWh),
                            new SqlParameter("@loc", toLoc),
                            new SqlParameter("@qty", qty)
                            },
                            conn, tran);
                    }
                    else
                    {
                        _db.ExecuteNonQuery(@"
                            UPDATE Stocks
                            SET Qty = Qty + @qty
                            WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                            new[] {
                            new SqlParameter("@qty", qty),
                            new SqlParameter("@item", itemId),
                            new SqlParameter("@wh", toWh),
                            new SqlParameter("@loc", toLoc)
                            },
                            conn, tran);
                    }

                    decimal balanceIn = destStock + qty;

                    // STOCK CARD IN
                    _db.ExecuteNonQuery(@"
                        INSERT INTO StockCards
                        (TransDate, ItemId, WarehouseId, LocationId, TransType, ReferenceNo, QtyIn, QtyOut, Balance)
                        VALUES
                        (GETDATE(), @item, @wh, @loc, 'TRANSFER_IN', @ref, @qty, 0, @balance)",
                        new[] {
                        new SqlParameter("@item", itemId),
                        new SqlParameter("@wh", toWh),
                        new SqlParameter("@loc", toLoc),
                        new SqlParameter("@qty", qty),
                        new SqlParameter("@balance", balanceIn),
                        new SqlParameter("@ref", h["TransferNumber"])
                        },
                        conn, tran);
                }

                // 6️⃣ UPDATE STATUS
                _db.ExecuteNonQuery(@"
                    UPDATE StockTransfers
                    SET Status = 'POSTED'
                    WHERE TransferId = @id",
                    new[] 
                    { 
                        new SqlParameter("@id", id) },
                    conn, tran);

                tran.Commit();
                TempData["Success"] = "Transfer berhasil diposting";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }


    }
}
