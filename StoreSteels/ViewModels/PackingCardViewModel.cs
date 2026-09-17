using StoreSteels.Helpers;
using StoreSteels.Models;
using StoreSteels.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace StoreSteels.ViewModels
{
    public class PackingCardViewModel : INotifyPropertyChanged
    {
        private readonly PackingCardErpService _erpService = new PackingCardErpService();
        private const int PageSize = 10;

        private List<PackingCardModel> _filteredItems = new List<PackingCardModel>();
        private int _revealedCount = 0;

        public UserSession CurrentUser { get; set; }

        // รายการที่แสดงจริงบนตาราง (ทยอยเพิ่มทีละ 10 แถวตอน Scroll กันข้อมูลเยอะแล้วเครื่องค้าง)
        public ObservableCollection<PackingCardModel> VisibleItems { get; } = new ObservableCollection<PackingCardModel>();

        public ICollectionView GroupedItems { get; private set; }

        public bool HasMoreToLoad => _revealedCount < _filteredItems.Count;

        private PackingCardModel _previewItem;
        public PackingCardModel PreviewItem
        {
            get => _previewItem;
            set { _previewItem = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public PackingCardViewModel()
        {
            GroupedItems = CollectionViewSource.GetDefaultView(VisibleItems);
            GroupedItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PackingCardModel.GroupCode)));

            LoadData();
        }

        private List<PackingCardModel> _allItems = new List<PackingCardModel>();

        public async void LoadData()
        {
            IsLoading = true;
            try
            {
                var data = await Task.Run(() => _erpService.GetPendingPackingCards());
                _allItems = data;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError("โหลดข้อมูล Packing Card ไม่สำเร็จ: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ApplyFilter()
        {
            string kw = (SearchText ?? "").Trim();

            _filteredItems = string.IsNullOrEmpty(kw)
                ? _allItems
                : _allItems.Where(x =>
                    (x.TicketNo?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.WorkOrder?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.LotNo?.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                  ).ToList();

            _revealedCount = 0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                VisibleItems.Clear();
                LoadMore();
            });
        }

        public void ClearSearch()
        {
            SearchText = string.Empty;
            ApplyFilter();
        }

        // เรียกตอน Scroll ใกล้ล่างสุด เพิ่มทีละ 10 แถว
        public void LoadMore()
        {
            if (!HasMoreToLoad) return;

            var next = _filteredItems.Skip(_revealedCount).Take(PageSize).ToList();
            foreach (var item in next)
            {
                VisibleItems.Add(item);
            }
            _revealedCount += next.Count;

            GroupedItems.Refresh();
            OnPropertyChanged(nameof(HasMoreToLoad));
        }

        public List<PackingCardModel> GetSelectedItems()
            => VisibleItems.Where(x => x.IsSelected).ToList();
    }
}
