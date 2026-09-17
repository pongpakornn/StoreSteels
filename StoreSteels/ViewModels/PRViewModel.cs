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

    namespace StoreSteels.ViewModels
    {
        public class PRViewModel : INotifyPropertyChanged
        {
            private readonly PRService _prService = new PRService();
            private readonly ExportService _exportService = new ExportService();
            public event PropertyChangedEventHandler PropertyChanged;

            public ObservableCollection<PRModel> PRHistory { get; set; } = new ObservableCollection<PRModel>();
            public ObservableCollection<string> ProductSuggestions { get; set; } = new ObservableCollection<string>();

            private UserSession _currentUser;
            public UserSession CurrentUser
            {
                get => _currentUser;
                set { _currentUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoginUserName)); }
            }

            public string LoginUserName => CurrentUser?.UserName ?? "Unknown User";
            public string CurrentDateDisplay => DateTime.Now.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            // ค่าคงที่ตามที่นนท์กำหนด
            public string FixedDept => "41304134-บำรุงรักษาแม่พิมพ์";
            public string FixedRemark => "ซ่อมแม่พิมพ์/ประตู2B";
            public string FixedTarget => "ชนนิกานต์";

        #region === [ Load Data Function ] ===

        protected void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public async Task LoadAllPR(string search = "")
            {
                try
                {
                    var data = await Task.Run(() => _prService.GetPRList(search));
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PRHistory.Clear();
                        foreach (var item in data) PRHistory.Add(item);
                    });
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError("โหลดข้อมูล PR ไม่สำเร็จ: " + ex.Message);
                }
            }

        #endregion

        #region === [ Save Function ] ===

        public async Task<bool> SavePRToDb(PRModel item)
            {
                // 1. เช็คว่า Login หรือยัง
                if (CurrentUser == null || string.IsNullOrEmpty(CurrentUser.UserId))
                {
                    DialogHelper.ShowWarning("กรุณาล็อกอินก่อนทำรายการ");
                    return false;
                }

                // 2. เช็ค Level (ถ้า Level > 2 คือไม่มีสิทธิ์สร้าง PR)
                if (CurrentUser.UserLevel > 3)
                {
                    DialogHelper.ShowWarning("คุณไม่มีสิทธิ์สร้างรายการ PR");
                    return false;
                }

                try
                {
                    string finalPRNo = "";
                    bool success = await Task.Run(() =>
                    {
                        finalPRNo = _prService.GetNextPRNo();
                        item.PR_DATE = DateTime.Now;
                        return _prService.InsertPR(finalPRNo, CurrentUser.UserId, FixedDept, item.PartName, item.QTY, FixedRemark, FixedTarget);
                    });

                    if (success)
                    {
                        LogService.WriteLog(CurrentUser.UserId, "CREATE_PR", $"Issued PR | NO: {finalPRNo}", finalPRNo);
                        await LoadAllPR();
                    }
                    return success;
                }
                catch (Exception ex)
                {
                    DialogHelper.ShowError("บันทึก PR ไม่สำเร็จ: " + ex.Message);
                    return false;
                }
            }

        #endregion

        #region === [ Approve Function ] ===

        public async Task ApprovePR(PRModel item)
            {
                if (item == null || CurrentUser == null) return;
                if (CurrentUser.UserLevel > 2)
                {
                    DialogHelper.ShowWarning("คุณไม่มีสิทธิ์ในการอนุมัติรายการนี้");
                    return;
                }

                try
                {
                    bool success = await Task.Run(() => _prService.UpdatePRStatus(item.PR_NO, "Approved", CurrentUser.UserId));
                    if (success)
                    {
                        // คืนชีพ LogService แบบเจาะจง PR
                        LogService.WritePRLog(CurrentUser.UserId, "APPROVE_PR", $"Approved PR No: {item.PR_NO}", item.PR_NO);
                        await LoadAllPR();
                    }
                }
                catch (Exception ex) { DialogHelper.ShowError("อนุมัติไม่สำเร็จ: " + ex.Message); }
            }

        #endregion

        #region === [ Reject Function ] ===

        public async Task RejectPR(PRModel item)
            {
                if (item == null || CurrentUser == null) return;

                // คืนค่าการ Confirm ก่อนลบ/ปฏิเสธ
                bool isConfirm = DialogHelper.ShowConfirm($"คุณต้องการ Reject รายการ {item.PR_NO} ใช่หรือไม่?", "ยืนยัน");
                if (!isConfirm) return;

                try
                {
                    bool success = await Task.Run(() => _prService.UpdatePRStatus(item.PR_NO, "Rejected", CurrentUser.UserId));
                    if (success)
                    {
                        // ใช้ WritePRLog เหมือนของเดิม
                        LogService.WritePRLog(CurrentUser.UserId, "REJECT_PR", $"Rejected PR No: {item.PR_NO}", item.PR_NO);
                        await LoadAllPR();
                    }
                }
                catch (Exception ex) { DialogHelper.ShowError("Reject ไม่สำเร็จ: " + ex.Message); }
            }

        #endregion

        #region === [ Export Function ] ===
        public async Task<bool> ProcessExportAsync(IEnumerable<PRModel> items)
            {
                if (CurrentUser == null || items == null || !items.Any()) return false;
                if (CurrentUser.UserLevel > 2)
                {
                    DialogHelper.ShowWarning("คุณไม่มีสิทธิ์ส่งออกข้อมูล");
                    return false;
                }

                try
                {
                    // 1. Export File
                    await Task.Run(() => _exportService.ExportPRToExcel(items));

                    // 2. Update DB & Log
                    await Task.Run(() =>
                    {
                        foreach (var item in items)
                        {
                            if (_prService.UpdateAfterExport(item.PR_NO, CurrentUser.UserId))
                            {
                                LogService.WriteLog(CurrentUser.UserId, "EXPORT_PR", $"Exported PR No: {item.PR_NO}", item.PR_NO);
                            }
                        }
                    });

                    await LoadAllPR();
                    return true;
                }
                catch (Exception ex)
                {
                    string msg = ex.Message.Contains("being used") ? "กรุณาปิดไฟล์ Excel ก่อน Export รายการ" : ex.Message;
                    DialogHelper.ShowError("Export ไม่สำเร็จ: " + msg);
                    return false;
                }
            }

        #endregion

        #region === [ Search & Suggestions ] ===

        public async Task UpdateSuggestions(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    Application.Current.Dispatcher.Invoke(() => ProductSuggestions.Clear());
                    return;
                }

                var data = await Task.Run(() => _prService.GetProductSuggestions(text));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // ป้องกัน UI กระพริบ: ถ้าข้อมูลเท่าเดิมไม่ต้องวาดใหม่
                    if (data.Count == ProductSuggestions.Count) return;

                    ProductSuggestions.Clear();
                    foreach (var item in data) ProductSuggestions.Add(item);
                });
            }

        #endregion

        #region === [ Select All Function ] ===

        public void ToggleSelectAll(bool isSelected)
            {
                foreach (var item in PRHistory) item.IsSelected = isSelected;
            }
        }

        #endregion
}