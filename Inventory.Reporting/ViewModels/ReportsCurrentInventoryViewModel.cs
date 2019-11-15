﻿using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Common.EntityLayer.Model.Entities;
using Inventory.Common.ApplicationLayer;
using DevExpress.Mvvm;
using Prism.Regions;
using Prism.Events;
using PrismCommands = Prism.Commands;
using System.Threading.Tasks;
using Inventory.Common.DataLayer.EntityDataManagers;
using System.Windows.Input;
using System.Windows;
using Inventory.Common.ApplicationLayer.Services;
using Inventory.Common.EntityLayer.Model;
using System.Collections.ObjectModel;
using Inventory.Common.BuisnessLayer;
using System.Data.Entity;
using System.IO;
using System.Diagnostics;
using System.Data;

namespace Inventory.Reporting.ViewModels {
    public class ReportsCurrentInventoryViewModel : InventoryViewModelBase {
        public IExportService ExportServiceTotalInventory { get => ServiceContainer.GetService<IExportService>("TotalInventoryExportService"); }
        public IDispatcherService DispatcherService { get => ServiceContainer.GetService<IDispatcherService>("ReportTotalsProductDispatcher"); }
        private InventoryContext _context;

        private ObservableCollection<TotalReportDataRow> _productTotals = new ObservableCollection<TotalReportDataRow>();
        private bool _isLoading = false;

        public AsyncCommand CollectProductTotalsCommand { get; set; }
        public AsyncCommand<ExportFormat> ExportProductTotalsCommand { get; set; }

        public ReportsCurrentInventoryViewModel(InventoryContext context) {
            this._context = context;
            this.CollectProductTotalsCommand = new AsyncCommand(this.CollectProductTotalsHandler);
            this.ExportProductTotalsCommand = new AsyncCommand<ExportFormat>(this.ExportProductTotalsHandler);
        }

        public override bool KeepAlive {
            get => true;
        }

        public ObservableCollection<TotalReportDataRow> ProductTotals {
            get => this._productTotals;
            set => SetProperty(ref this._productTotals, value, "ProductTotals");
        }

        public bool IsLoading {
            get => this._isLoading;
            set => SetProperty(ref this._isLoading, value, "IsLoading");
        }

        private async Task CollectProductTotalsHandler() {
            ObservableCollection<TotalReportDataRow> summary = new ObservableCollection<TotalReportDataRow>();
            double total = 0;
            this.IsLoading = true;
            var products = await this._context.InventoryItems.OfType<Product>()
                .AsNoTracking().Include(e => e.Lots.Select(x => x.ProductInstances))
                .Include(e => e.Lots.Select(x => x.Cost)).ToListAsync();

            await Task.Run(() => {
                foreach (var product in products) {
                    var row = new TotalReportDataRow();
                    var pTotal = product.Lots.Sum(lot => {
                        var quantity = lot.ProductInstances.Sum(rank => rank.Quantity);
                        if (lot.Cost != null) {
                            return quantity * lot.Cost.Amount;
                        } else {
                            var cost = this._context.Rates.OfType<Cost>()
                                        .Include(e => e.Lot)
                                        .FirstOrDefault(x => x.LotNumber == lot.LotNumber && x.SupplierPoNumber == lot.SupplierPoNumber);
                            if (cost != null) {
                                return quantity * cost.Amount;
                            } else {
                                return quantity * 0;
                            }
                        }
                    });
                    total += pTotal;
                    row.Product = product;
                    row.TotalCost = pTotal;
                    summary.Add(row);
                }
            });
            this.ProductTotals = summary;
            this.IsLoading = false;
        }

        private async Task ExportProductTotalsHandler(ExportFormat format) {
            await Task.Run(() => {
                this.DispatcherService.BeginInvoke(() => {
                    var path = Path.ChangeExtension(Path.GetTempFileName(), format.ToString().ToLower());
                    using (FileStream file = File.Create(path)) {
                        this.ExportServiceTotalInventory.Export(file, format, new DevExpress.XtraPrinting.XlsxExportOptionsEx() {
                            //ExportType = DevExpress.Export.ExportType.WYSIWYG
                        });
                    }
                    Process.Start(path);
                });
            });
        }
    }
}