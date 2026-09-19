<p align="center">
  <strong>-------></strong>
  <a href="/README.md">Russian</a> |
  <a href="/README.en.md">English</a> |
  <a href="/README.es.md">Spanish</a> |
  <a href="/README.zh.md">Chinese</a> |
  <strong><-------</strong>
</p>

# ZCode Desktop 俄语汉化补丁（俄语本地化）

面向 ZCode Desktop（AI 编程智能体的图形界面客户端）的非官方俄语（ru-RU）
本地化。官方版本只带英文和中文界面；本项目提供完整的俄语界面：设置、菜单、
对话框、系统托盘、"关于"窗口（约 4900 条字符串）。

适用于**任意版本**的客户端：安装器会自行解包 `app.asar`，按代码特征定位
文件并打补丁。新版本中尚未翻译的字符串会显示为英文，不会损坏任何功能。

## 安装（自动）

1. 完全退出 ZCode（包括托盘图标）。
2. 从 [Releases](../../releases) 页面下载压缩包
   （`zcode-ru-localization-vX.X.X.zip`），解压后运行
   **`РусификаторZCode.exe`**。
3. 选择 ZCode 所在文件夹 → 点击 "Установить русский язык"（安装俄语）。
4. 启动 ZCode，界面即为俄语。

无图形界面的替代方式 —— PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File install-ru.ps1 [-InstallDir "C:\ZCode路径"]
```

依赖：PATH 中的 [Node.js](https://nodejs.org)。

## 卸载（还原）

使用程序中的 "Откатить"（回滚）按钮，或手动删除 `resources\app` 并将
`resources\app.asar.orig` 重命名回 `resources\app.asar`。

## 客户端更新之后

客户端更新会恢复英文界面。重新运行安装器即可将补丁应用到新版本；未翻译
的字符串会显示英文。

## 工作原理

- 若 `resources/app` 文件夹存在，Electron 会优先加载它而不是 `app.asar`。
  安装器不重新打包归档，而是将补丁后的文件夹放在旁边（原始文件保留为
  `app.asar.orig`）。
- `patch.mjs` 通过特征 `g={"zh-CN":…,"en-US":…}` 定位本地化目录（文件名随
  版本变化），注入俄语目录，并将 `ru-RU` 加入校验规则、语言选择器以及
  托盘与"关于"目录。
- 安装前会对补丁后的文件进行语法检查；任何错误都会中止安装，客户端保持
  原样。

## 参与翻译

所有字符串都在 [`ru.json`](ru.json)（键 → 文本）。发现未翻译的内容时，
请添加条目并重新运行安装器。请勿修改占位符（`{version}`、`{count}`）。

## 从源码构建安装器

exe 使用 Windows 自带的 .NET Framework 编译器编译：运行 `Build.bat` 或：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -out:"РусификаторZCode.exe" InstallRu.cs -r:System.Windows.Forms.dll -r:System.Drawing.dll
```

exe 旁边需要 `patch.mjs`、`ru.json` 和 `node_modules`
（`npm install @electron/asar`）。

## 免责声明

非官方项目，与 ZCode 开发者无关。仅修改本机上的本地化文件。作者对可能
出现的问题不承担责任——随时可以回滚（见上文）。

## 许可证

[MIT](LICENSE)
