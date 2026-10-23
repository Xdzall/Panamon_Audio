using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.Shared
{
    public class SutModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;

        private static readonly HashSet<string> ValidAudioLines = new(StringComparer.OrdinalIgnoreCase)
        {
            "FINAL1", "FINAL2", "FINAL3", "FINAL4", "FINAL5",
            "FINAL6", "FINAL7", "FINAL8", "FINAL9", "FINAL10", "FINAL11"
        };

        public SutModel(ScaffoldedDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? FilterMachineCode { get; set; }

        public List<ProductSut> listProducts { get; set; } = new();
        public List<UnregisteredModelItem> listUnregisteredModels { get; set; } = new();

        public class UnregisteredModelItem
        {
            public string MachineCode { get; set; } = "";
            public string Model { get; set; } = "";
            public int? ObservedSut { get; set; }
            public int SnapshotCount { get; set; }
        }

        // ─── GET ───────────────────────────────────────────────
        public async Task OnGetAsync()
        {
            listProducts = new List<ProductSut>();
            listUnregisteredModels = new List<UnregisteredModelItem>();

            var conn = _context.Database.GetDbConnection();
            try
            {
                await conn.OpenAsync();

                // 1. Ambil data produk terdaftar dari MasterData
                using (var cmd = conn.CreateCommand())
                {
                    if (string.IsNullOrEmpty(FilterMachineCode))
                    {
                        cmd.CommandText = "SELECT Product_Id, ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour FROM MasterData ORDER BY MachineCode, ProductName";
                    }
                    else
                    {
                        cmd.CommandText = "SELECT Product_Id, ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour FROM MasterData WHERE MachineCode = @mc ORDER BY ProductName";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@mc";
                        p.Value = FilterMachineCode;
                        cmd.Parameters.Add(p);
                    }

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        listProducts.Add(new ProductSut
                        {
                            Product_Id = reader["Product_Id"]?.ToString(),
                            ProductName = reader["ProductName"]?.ToString(),
                            MachineCode = reader["MachineCode"]?.ToString(),
                            Description = reader["Description"]?.ToString(),
                            ProdPlan = reader["ProdPlan"] == DBNull.Value ? null : Convert.ToInt32(reader["ProdPlan"]),
                            SUT = reader["SUT"] == DBNull.Value ? null : Convert.ToInt32(reader["SUT"]),
                            NoOfOperator = reader["NoOfOperator"] == DBNull.Value ? null : Convert.ToInt32(reader["NoOfOperator"]),
                            QtyHour = reader["QtyHour"] == DBNull.Value ? null : Convert.ToInt32(reader["QtyHour"]),
                            ProdHeadHour = reader["ProdHeadHour"] == DBNull.Value ? null : Convert.ToInt32(reader["ProdHeadHour"]),
                            CycleTimeVacum = reader["CycleTimeVacum"] == DBNull.Value ? null : Convert.ToInt32(reader["CycleTimeVacum"]),
                            WorkHour = reader["WorkHour"] == DBNull.Value ? null : Convert.ToInt32(reader["WorkHour"]),
                        });
                    }
                }

                // 2. Deteksi model dari OEESN yang BELUM TERDAFTAR di MasterData
                using (var cmdUnreg = conn.CreateCommand())
                {
                    cmdUnreg.CommandText = @"
                        WITH ProdModels AS (
                            SELECT 'FINAL1' AS MachineCode, Model, MAX(Sut) AS ObservedSut, COUNT(*) AS SnapshotCount FROM dbo.FINAL1 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL2', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL2 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL3', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL3 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL4', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL4 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL5', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL5 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL7', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL7 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL8', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL8 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL9', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL9 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                            UNION ALL SELECT 'FINAL10', Model, MAX(Sut), COUNT(*) FROM dbo.FINAL10 WHERE Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> '' GROUP BY Model
                        )
                        SELECT pm.MachineCode, pm.Model, pm.ObservedSut, pm.SnapshotCount
                        FROM ProdModels pm
                        LEFT JOIN MasterData md ON pm.Model = md.ProductName AND pm.MachineCode = md.MachineCode
                        WHERE md.ProductName IS NULL
                        ORDER BY pm.MachineCode, pm.SnapshotCount DESC";

                    using var unregReader = await cmdUnreg.ExecuteReaderAsync();
                    while (await unregReader.ReadAsync())
                    {
                        listUnregisteredModels.Add(new UnregisteredModelItem
                        {
                            MachineCode = unregReader["MachineCode"]?.ToString() ?? "",
                            Model = unregReader["Model"]?.ToString() ?? "",
                            ObservedSut = unregReader["ObservedSut"] == DBNull.Value ? null : Convert.ToInt32(unregReader["ObservedSut"]),
                            SnapshotCount = Convert.ToInt32(unregReader["SnapshotCount"])
                        });
                    }
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        // ─── INSERT (TERVALIDASI) ──────────────────────────────
        public async Task<IActionResult> OnPostInsertAsync(
            string? FilterMachineCode,
            string? ProductName, string? MachineCode, string? Description,
            int? ProdPlan, int? SUT, int? NoOfOperator, int? QtyHour,
            int? ProdHeadHour, int? CycleTimeVacum, int? WorkHour)
        {
            try
            {
                var cleanName = ProductName?.Trim();
                var cleanLine = MachineCode?.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "Product Name wajib diisi.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                if (string.IsNullOrWhiteSpace(cleanLine) || !ValidAudioLines.Contains(cleanLine))
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = $"Lini '{cleanLine}' tidak valid. Harus memilih salah satu lini Audio FINAL1 s.d. FINAL11.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                if (cleanLine == "FINAL6" || cleanLine == "FINAL11")
                {
                    // Izinkan pendaftaran, namun beri catatan status standby
                    Description = string.IsNullOrWhiteSpace(Description)
                        ? "Lini Standby / Cadangan"
                        : $"{Description} (Lini Standby)";
                }

                if (SUT == null || SUT <= 0)
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "SUT (Standard Unit Time) wajib diisi bilangan bulat positif dalam satuan detik (> 0).";
                    return RedirectToPage(new { FilterMachineCode });
                }

                if (ProdPlan == null || ProdPlan <= 0)
                {
                    ProdPlan = 1000; // Default kapasitas harian Audio
                }

                if (NoOfOperator == null || NoOfOperator <= 0)
                {
                    NoOfOperator = 25; // Default alokasi operator lini Audio
                }

                WorkHour ??= 7;
                QtyHour ??= (int)Math.Round((double)ProdPlan / Math.Max(1, WorkHour.Value));
                ProdHeadHour ??= 4;

                // 1. Simpan ke MasterData
                await _context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO MasterData (ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                    cleanName, cleanLine,
                    Description ?? (object)DBNull.Value,
                    ProdPlan,
                    SUT,
                    NoOfOperator,
                    QtyHour,
                    ProdHeadHour,
                    CycleTimeVacum ?? (object)DBNull.Value,
                    WorkHour);

                // 2. Sinkronkan ke tabel Product (menjamin kedua master selalu sinkron)
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM Product WHERE ProductName = {0} AND MachineCode = {1})
                    BEGIN
                        INSERT INTO Product (ProductName, MachineCode, Description, Marking, Product_Id)
                        VALUES ({0}, {1}, {2}, '1', '1');
                    END", cleanName, cleanLine, Description ?? (object)DBNull.Value);

                TempData["StatusMessage"] = "success";
                TempData["Message"] = $"Product '{cleanName}' untuk lini '{cleanLine}' berhasil ditambahkan ke Master Data & Product.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal menambahkan: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }

        // ─── UPDATE (TERVALIDASI) ──────────────────────────────
        public async Task<IActionResult> OnPostUpdateAsync(
            string? FilterMachineCode,
            string? ProductId,
            string? OriginalProductName,
            string? OriginalMachineCode,
            string? ProductName, string? MachineCode, string? Description,
            int? ProdPlan, int? SUT, int? NoOfOperator, int? QtyHour,
            int? ProdHeadHour, int? CycleTimeVacum, int? WorkHour)
        {
            try
            {
                var cleanName = ProductName?.Trim();
                var cleanLine = MachineCode?.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "Product Name wajib diisi.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                if (string.IsNullOrWhiteSpace(cleanLine) || !ValidAudioLines.Contains(cleanLine))
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = $"Lini '{cleanLine}' tidak valid. Harus memilih salah satu lini Audio FINAL1 s.d. FINAL11.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                if (SUT == null || SUT <= 0)
                {
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "SUT (Standard Unit Time) wajib diisi bilangan bulat positif dalam satuan detik (> 0).";
                    return RedirectToPage(new { FilterMachineCode });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();
                int affectedRows;

                if (!string.IsNullOrWhiteSpace(ProductId))
                {
                    affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE MasterData SET
                            ProductName    = {cleanName},
                            MachineCode    = {cleanLine},
                            Description    = {Description},
                            ProdPlan       = {ProdPlan},
                            SUT            = {SUT},
                            NoOfOperator   = {NoOfOperator},
                            QtyHour        = {QtyHour},
                            ProdHeadHour   = {ProdHeadHour},
                            CycleTimeVacum = {CycleTimeVacum},
                            WorkHour       = {WorkHour}
                        WHERE Product_Id = {ProductId}");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(OriginalProductName))
                    {
                        await transaction.RollbackAsync();
                        TempData["StatusMessage"] = "error";
                        TempData["Message"] = "Data tidak memiliki identitas yang valid dan tidak dapat diupdate.";
                        return RedirectToPage(new { FilterMachineCode });
                    }

                    affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE MasterData SET
                            ProductName    = {cleanName},
                            MachineCode    = {cleanLine},
                            Description    = {Description},
                            ProdPlan       = {ProdPlan},
                            SUT            = {SUT},
                            NoOfOperator   = {NoOfOperator},
                            QtyHour        = {QtyHour},
                            ProdHeadHour   = {ProdHeadHour},
                            CycleTimeVacum = {CycleTimeVacum},
                            WorkHour       = {WorkHour}
                        WHERE (Product_Id IS NULL OR LTRIM(RTRIM(Product_Id)) = '')
                          AND ProductName = {OriginalProductName}
                          AND ((MachineCode = {OriginalMachineCode})
                               OR (MachineCode IS NULL AND {OriginalMachineCode} IS NULL))");
                }

                if (affectedRows < 1)
                {
                    await transaction.RollbackAsync();
                    TempData["StatusMessage"] = "error";
                    TempData["Message"] = "Data tidak ditemukan. Tidak ada perubahan yang disimpan.";
                    return RedirectToPage(new { FilterMachineCode });
                }

                // Sinkronkan ke Product
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Product SET
                        ProductName = {cleanName},
                        MachineCode = {cleanLine},
                        Description = {Description}
                    WHERE ProductName = {OriginalProductName ?? cleanName}
                      AND MachineCode = {OriginalMachineCode ?? cleanLine}");

                await transaction.CommitAsync();

                TempData["StatusMessage"] = "success";
                TempData["Message"] = $"Product '{cleanName}' untuk lini '{cleanLine}' berhasil diperbarui.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal update: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }

        // ─── DELETE (TERVALIDASI) ──────────────────────────────
        public async Task<IActionResult> OnPostDeleteAsync(
            string? FilterMachineCode,
            string? ProductId,
            string? ProductName,
            string? MachineCode)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ProductId))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM MasterData WHERE Product_Id = {0}", ProductId);
                }
                else if (!string.IsNullOrWhiteSpace(ProductName) && !string.IsNullOrWhiteSpace(MachineCode))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM MasterData WHERE ProductName = {0} AND MachineCode = {1}", ProductName, MachineCode);
                }

                // Sinkronkan hapus dari Product
                if (!string.IsNullOrWhiteSpace(ProductName) && !string.IsNullOrWhiteSpace(MachineCode))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM Product WHERE ProductName = {0} AND MachineCode = {1}", ProductName, MachineCode);
                }

                TempData["StatusMessage"] = "success";
                TempData["Message"] = "Product berhasil dihapus dari Master Data & Product.";
            }
            catch (Exception ex)
            {
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Gagal hapus: {ex.Message}";
            }
            return RedirectToPage(new { FilterMachineCode });
        }
    }
}
