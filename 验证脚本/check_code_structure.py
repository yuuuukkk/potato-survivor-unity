# -*- coding: utf-8 -*-
"""《土豆幸存者》代码一致性检查（两遍扫描）：
第一遍收集全部定义类型 + 命名空间/目录对应；
第二遍检查 GetComponent<T>/AddComponent<T>/RequireComponent 引用的类型存在。
Unity 内置组件列入白名单。
"""
import os, re, sys

ROOT = r"D:\wenjian\rougelike\UnityProject\Assets\_Game\Scripts"
errors = []
defined = {}          # name -> file
ns_dir_map = {}       # namespace -> set(dirname)
files = []            # (path, src)

TYPE_DECL = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial)\s+)*(class|enum|interface|struct)\s+([A-Za-z_]\w*)', re.M)
GEN_REF = re.compile(r'(?:GetComponent|AddComponent|GetComponentInChildren|GetComponentInParent)<\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)')
REQUIRE = re.compile(r'RequireComponent\(\s*typeof\(\s*([A-Za-z_]\w*)\s*\)')

# Unity / .NET 内置类型白名单
BUILTIN = {
    "SpriteRenderer", "CircleCollider2D", "Rigidbody2D", "Camera", "AudioListener",
    "Text", "Image", "Button", "RectTransform", "Canvas", "CanvasScaler",
    "GraphicRaycaster", "EventSystem", "StandaloneInputModule", "Collider2D",
    "Transform", "GameObject", "MonoBehaviour", "TextAsset", "Texture2D", "Font",
    "TextAnchor", "HorizontalWrapMode", "Vector2", "Vector3", "Color", "Rect", "Mathf",
    "Random", "Time", "Object", "Debug", "Application", "Resources", "ColorUtility",
    "Physics2D", "WaitForSeconds", "Coroutine", "CanvasGroup", "Sprite",
    "EventTrigger", "EventTriggerType", "KeyCode", "AudioSource", "BoxCollider2D",
    "Slider", "Outline",
}

# 第一遍：收集
for dirpath, _, names in os.walk(ROOT):
    for fn in names:
        if not fn.endswith(".cs"): continue
        p = os.path.join(dirpath, fn)
        with open(p, "r", encoding="utf-8") as f:
            src = f.read()
        files.append((p, src))
        dirname = os.path.basename(dirpath)
        m = re.search(r'namespace\s+([\w.]+)', src)
        if m:
            ns_dir_map.setdefault(m.group(1), set()).add(dirname)
        for dm in TYPE_DECL.finditer(src):
            name = dm.group(2)
            if name in defined:
                errors.append(f"类型重复定义: {name} ({defined[name]} 与 {p})")
            else:
                defined[name] = p

# 第二遍：检查引用
for p, src in files:
    for rm in GEN_REF.finditer(src):
        t = rm.group(1).rsplit(".", 1)[-1]
        if t not in defined and t not in BUILTIN:
            errors.append(f"{p}: 组件类型 {t} 未找到定义")
    for rm in REQUIRE.finditer(src):
        t = rm.group(1)
        if t not in defined and t not in BUILTIN:
            errors.append(f"{p}: RequireComponent {t} 未找到定义")

# namespace 与目录一致性
for ns, dirs in ns_dir_map.items():
    tail = ns.rsplit(".", 1)[-1]
    for d in dirs:
        if d != tail and not (ns == "RogueLike.Core" and d == "Config"):
            errors.append(f"namespace {ns} 位于目录 {d}（应 {tail}）")

print(f"定义类型: {len(defined)} 个  检查文件: {len(files)} 个")
print(f"命名空间: {sorted(ns_dir_map.keys())}")
print("\n========== 结果 ==========")
print(f"错误: {len(errors)}")
for e in errors:
    print(" [ERR]", e)
sys.exit(1 if errors else 0)
