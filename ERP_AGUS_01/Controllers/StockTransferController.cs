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
            // =============================
            // 0️⃣ VALIDASI AWAL (TANPA DB)
            // =============================
            if (model.Items == null || !model.Items.Any())
            {
                TempData["DraftError"] = "Item belum diisi";
                return RedirectToAction("Index");
            }

            if (model.FromWarehouseId == model.ToWarehouseId &&
                model.FromLocationId == model.ToLocationId)
            {
                TempData["DraftError"] = "Gudang & lokasi asal dan tujuan tidak boleh sama";
                return RedirectToAction("Index");
            }

            // cek duplikat item
            var duplicate = model.Items
                .GroupBy(x => x.ItemId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                TempData["DraftError"] = "Item tidak boleh duplikat";
                return RedirectToAction("Index");
            }

            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // =============================
                // 1️⃣ VALIDASI STOCK
                // =============================
                foreach (var item in model.Items)
                {
                    if (item.Qty <= 0)
                        throw new Exception("Qty harus lebih dari 0");

                    decimal stock = Convert.ToDecimal(
                        _db.ExecuteScalar(@"
                    SELECT ISNULL(Qty,0)
                    FROM Stocks
                    WHERE ItemId=@item
                      AND WarehouseId=@wh
                      AND LocationId=@loc",
                            new[]
                            {
                        new SqlParameter("@item", item.ItemId),
                        new SqlParameter("@wh", model.FromWarehouseId),
                        new SqlParameter("@loc", model.FromLocationId)
                            },
                            conn, tran)
                    );

                    if (stock < item.Qty)
                        throw new Exception(
                            $"Stock tidak mencukupi untuk ItemId {item.ItemId} (Available: {stock})");
                }

                // =============================
                // 2️⃣ INSERT HEADER
                // =============================
                string transferNo = "TRF-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                int transferId = Convert.ToInt32(_db.ExecuteScalar(@"
            INSERT INTO StockTransfers
            (
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
                'DRAFT'
            );
            SELECT SCOPE_IDENTITY();",
                    new[]
                    {
                new SqlParameter("@TransferNumber", transferNo),
                new SqlParameter("@FromWarehouseId", model.FromWarehouseId),
                new SqlParameter("@ToWarehouseId", model.ToWarehouseId),
                new SqlParameter("@FromLocationId", model.FromLocationId),
                new SqlParameter("@ToLocationId", model.ToLocationId)
                    },
                    conn, tran));

                // =============================
                // 3️⃣ INSERT DETAIL
                // =============================
                foreach (var item in model.Items)
                {
                    _db.ExecuteNonQuery(@"
                INSERT INTO StockTransferDetails
                (TransferId, ItemId, Qty)
                VALUES
                (@TransferId, @ItemId, @Qty)",
                        new[]
                        {
                    new SqlParameter("@TransferId", transferId),
                    new SqlParameter("@ItemId", item.ItemId),
                    new SqlParameter("@Qty", item.Qty)
                        },
                        conn, tran);
                }

                tran.Commit();
                TempData["Success"] = "Draft Transfer berhasil disimpan";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["DraftError"] = ex.Message;
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
                // 1️⃣ HEADER
                DataTable header = _db.ExecuteQuery(@"
            SELECT *
            FROM StockTransfers WITH (UPDLOCK, HOLDLOCK)
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

                if (details.Rows.Count == 0)
                    throw new Exception("Transfer tidak memiliki item");

                int fromWh = Convert.ToInt32(h["FromWarehouseId"]);
                int fromLoc = Convert.ToInt32(h["FromLocationId"]);
                int toWh = Convert.ToInt32(h["ToWarehouseId"]);
                int toLoc = Convert.ToInt32(h["ToLocationId"]);

                foreach (DataRow d in details.Rows)
                {
                    int itemId = Convert.ToInt32(d["ItemId"]);
                    decimal qty = Convert.ToDecimal(d["Qty"]);

                    if (qty <= 0)
                        throw new Exception("Qty transfer harus lebih dari 0");

                    // 3️⃣ CEK STOCK ASAL
                    decimal currentStock = Convert.ToDecimal(
                        _db.ExecuteScalar(@"
                                        SELECT ISNULL(SUM(Qty),0)
                                        FROM Stocks WITH (UPDLOCK)
                                        WHERE ItemId=@item AND WarehouseId=@wh AND LocationId=@loc",
                            new[] {
                                    new SqlParameter("@item", itemId),
                                    new SqlParameter("@wh", fromWh),
                                    new SqlParameter("@loc", fromLoc)
                            },
                            conn, tran)
                    );

                    if (currentStock < qty)
                        throw new Exception($"🚨 Stock tidak mencukupi (ItemId {itemId} - {currentStock})");

                    // =========================
                    // 4️⃣ STOCK KELUAR
                    // =========================
                    int affected = _db.ExecuteNonQuery(@"
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

                    if (affected == 0)
                        throw new Exception("Data stock asal tidak ditemukan");

                    // STOCK CARD OUT (TANPA BALANCE)
                    _db.ExecuteNonQuery(@"
                INSERT INTO StockCards
                (TransDate, ItemId, WarehouseId, LocationId, TransType, ReferenceNo, QtyIn, QtyOut)
                VALUES
                (GETDATE(), @item, @wh, @loc, 'TRANSFER_OUT', @ref, 0, @qty)",
                        new[] {
                    new SqlParameter("@item", itemId),
                    new SqlParameter("@wh", fromWh),
                    new SqlParameter("@loc", fromLoc),
                    new SqlParameter("@qty", qty),
                    new SqlParameter("@ref", h["TransferNumber"])
                        },
                        conn, tran);

                    // =========================
                    // 5️⃣ STOCK MASUK
                    // =========================
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

                    // STOCK CARD IN (TANPA BALANCE)
                    _db.ExecuteNonQuery(@"
                INSERT INTO StockCards
                (TransDate, ItemId, WarehouseId, LocationId, TransType, ReferenceNo, QtyIn, QtyOut)
                VALUES
                (GETDATE(), @item, @wh, @loc, 'TRANSFER_IN', @ref, @qty, 0)",
                        new[] {
                    new SqlParameter("@item", itemId),
                    new SqlParameter("@wh", toWh),
                    new SqlParameter("@loc", toLoc),
                    new SqlParameter("@qty", qty),
                    new SqlParameter("@ref", h["TransferNumber"])
                        },
                        conn, tran);
                }

                // 6️⃣ UPDATE STATUS
                _db.ExecuteNonQuery(@"
            UPDATE StockTransfers
            SET Status = 'POSTED'
            WHERE TransferId = @id",
                    new[] { new SqlParameter("@id", id) },
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
