"""
Dò và xóa các <Compile Include="..."> bị trùng lặp trong file .csproj
(kiểu cũ, không phải SDK-style) — nguyên nhân gây lỗi MSBuild:
    "Source file '...' specified multiple times"

CÁCH DÙNG:
    python fix_duplicate_compile.py "H:\\95 - Project\\00. FVN SYS\\PCSYS\\PCTP\\PCTP.csproj"

Script sẽ:
  1. Tạo file backup .csproj.bak_YYYYMMDD_HHMMSS trước khi sửa.
  2. Với mỗi Include bị trùng, GIỮ LẠI bản có nhiều thẻ con nhất
     (ví dụ bản có <SubType>/<DependentUpon> của UserControl/Designer),
     xóa các bản còn lại (thường là bản trống <Compile Include="..." />).
  3. In ra danh sách các Include đã xử lý để bạn kiểm tra lại.
"""
import sys
import shutil
import datetime
import xml.etree.ElementTree as ET

NS = "http://schemas.microsoft.com/developer/msbuild/2003"


def local(tag):
    return tag.split('}')[-1] if '}' in tag else tag


def main(csproj_path):
    ET.register_namespace('', NS)
    tree = ET.parse(csproj_path)
    root = tree.getroot()

    ns = {'m': NS}
    # Một số .csproj cũ không khai báo namespace -> thử fallback không ns
    compile_elems = root.findall(".//m:Compile", ns)
    if not compile_elems:
        compile_elems = [e for e in root.iter() if local(e.tag) == "Compile"]

    seen = {}  # include_path (lowercase) -> element được giữ lại
    to_remove = []  # (parent, element) cần xóa

    parent_map = {c: p for p in root.iter() for c in p}

    for elem in compile_elems:
        include = elem.get("Include")
        if not include:
            continue
        key = include.strip().lower()

        if key not in seen:
            seen[key] = elem
            continue

        # Đã gặp trước đó -> so sánh "độ đầy đủ" (số thẻ con), giữ bản nhiều hơn
        existing = seen[key]
        existing_children = len(list(existing))
        current_children = len(list(elem))

        if current_children > existing_children:
            # Bản mới đầy đủ hơn -> loại bản cũ, giữ bản mới
            to_remove.append((parent_map[existing], existing))
            seen[key] = elem
        else:
            # Giữ bản cũ, loại bản hiện tại
            to_remove.append((parent_map[elem], elem))

    if not to_remove:
        print("Không tìm thấy Compile Include nào bị trùng lặp.")
        return

    backup_path = f"{csproj_path}.bak_{datetime.datetime.now():%Y%m%d_%H%M%S}"
    shutil.copyfile(csproj_path, backup_path)
    print(f"Đã tạo backup: {backup_path}")

    removed_names = []
    for parent, elem in to_remove:
        parent.remove(elem)
        removed_names.append(elem.get("Include"))

    tree.write(csproj_path, encoding="utf-8", xml_declaration=True)

    print(f"\nĐã xóa {len(removed_names)} dòng <Compile Include> trùng lặp:")
    for name in removed_names:
        print(f"  - {name}")

    print("\nMở lại Visual Studio, Reload Project (nếu đang mở), build lại.")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print('Cách dùng: python fix_duplicate_compile.py "duong_dan_toi\\PCTP.csproj"')
        sys.exit(1)
    main(sys.argv[1])
