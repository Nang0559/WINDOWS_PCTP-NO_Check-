# PCTP UI Automation Control Catalog

Branch: `feature/pctp-qc-test-platform`

This catalog is derived from the WinForms source (not guessed screen captions). The menu item `Name` values are the stable UI Automation identifiers expected by the UI journey runner when the DevExpress accessibility provider exposes them as AutomationId.

## Shell navigation

| Area | WinForms control Name / AutomationId | Source caption | Route |
|---|---|---|---|
| Receiving menu group | `NHAccordionControlElement` | Nhập Kho & QC | `receive-navigation.json` |
| Receiving QR | `E_NhapTP` | Nhập thành phẩm - QR | `receive-navigation.json` |
| Delivery menu group | `accordionControlElement22` | Giao Hàng | delivery journeys |
| HVN delivery | `E_GHHVN_MP` | HVN - MP | `delivery-hvn-navigation.json` |
| Defect/rework menu group | `E_Trahang` | Xử lý hàng lỗi | QC/rework journeys |
| Rework worklist | `trahangndHVN` | Quản lý tiến trình | `rework-navigation.json` |
| QC direction queue | `accordionControlElement_QCDinhHuong` | QC chờ định hướng | `qc-direction-navigation.json` |
| QC final queue | `accordionControlElement_QCXacNhanCuoi` | QC chờ xác nhận cuối | `qc-final-navigation.json` |
| Warehouse map | `cmdRackControl` | Bản đồ kho | Manual discovery required before slot export journey |

## Screen/control identifiers verified in source

| Screen | Form Name | Window title | Child controls observed in source |
|---|---|---|---|
| Shell | `Main_APP` | `PCTP - WMS` | `accordionControl`, `ribbonControl`, `btHelp` |
| Receiving worklist | `FormNhapKhoTienTrinh` | Quản lý tiến trình Nhập kho | `receive_btnRefresh`, `receive_btnScan`, `receive_worklistGrid` |
| Warehouse canvas | `MainStockSV` | MainStock | `btnEnterItem`, `btnRegisterRack`, `PEditInput`, `pnlMain` |
| Slot export | `ExportFormSV` | XUẤT KHO - FVN | `slotExport_quantity`, `slotExport_btnPick`, `slotExport_btnPrint`, `slotExport_btnCancel`, `slotExport_btnCancelPending` |
| Customer delivery | `HVN_PGH` | HVN_PGH | `txt_DOCQRCODE`, `gridCtrDONHANG`, `gridCtrDOCQrCODE`, `UIButtonHOME` |
| Defect/rework worklist | `FormQuanLyTienTrinhHangLoi` | Quản lý tiến trình Xử lý hàng lỗi | `rework_search`, `rework_btnPrimaryAction`, `rework_btnCreateAbnormal`, `rework_worklistGrid`, `rework_step_01` … `rework_step_08` |
| QC direction | `FormQCDinhHuong` | QC định hướng xử lý hàng lỗi | `qcDirection_disposition`, `qcDirection_note`, `qcDirection_operator`, `qcDirection_btnConfirm`, `qcDirection_btnClose` |
| QC final | `FormQCXacNhanCuoi` | QC xác nhận cuối | `qcFinal_qtyOK`, `qcFinal_qtyNG`, `qcFinal_reworkLot`, `qcFinal_note`, `qcFinal_operator`, `qcFinal_btnConfirm`, `qcFinal_btnClose` |

## Important limitations

- DevExpress controls may expose different UIA patterns depending on the installed DevExpress accessibility provider. The runner fails with a diagnostic if an element is present but does not expose Invoke/SelectionItem.
- Some screens create controls dynamically; their runtime AutomationId values must be captured from `pctp-ui-tree-*.json` on the target workstation before adding action steps.
- Slot export is opened by double-clicking a physical slot on `MainStockSV` (see `MainStockSV.Canvas.cs`). The current journey engine intentionally does not synthesize screen coordinates or select an arbitrary slot. A disposable, pre-seeded slot and a stable slot AutomationId are required.
- Navigation manifests are non-mutating smoke journeys. They prove screen access only; they do not claim that a receipt, export, delivery, QC decision, or rework transaction was committed correctly.
