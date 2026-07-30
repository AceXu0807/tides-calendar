[README.md](https://github.com/user-attachments/files/30555898/README.md)
# 潮汐日历

一款半透明、可贴合 Windows 桌面壁纸使用的本地日历与待办软件。

潮汐日历使用原生 WinForms 编写，不依赖浏览器，也不需要联网。日程、窗口位置和外观设置均保存在本机。

## 功能特点

- 半透明桌面日历，可自由拖动和缩放
- Win11 风格的浅色、深色和海蓝主题
- 支持自定义背景色、文字颜色、字号和 0%～100% 透明度
- 显示农历日期，农历文字位于阳历日期右侧
- 双击日期打开独立待办窗口
- 待办窗口可拖动，并支持新增、完成和删除待办
- 每项待办可设置独立的高亮下划线颜色
- 使用霞鹭文楷字体，字体已嵌入可执行文件，无需单独安装
- 支持置顶、托盘隐藏、鼠标穿透和开机启动
- 单实例运行；再次双击 EXE 可恢复已经隐藏的窗口
- 自动保存窗口位置、尺寸、主题、透明度和待办数据

## 快速开始

1. 下载或获取 `潮汐日历.exe`。
2. 双击 EXE，日历会直接显示在桌面上。
3. 双击任意日期格，打开当天的待办窗口。
4. 在日历上点击鼠标右键，可以调整主题、底色、文字、透明度和其他行为。

本软件是本地程序，不需要安装，也不会连接网络。

## 基本操作

| 操作 | 功能 |
| --- | --- |
| 双击日期 | 打开当天的待办窗口 |
| 拖动顶部区域 | 移动日历 |
| 拖动四边或四角 | 调整日历大小和比例 |
| 点击顶部左右箭头 | 切换月份 |
| 点击“回到今天” | 返回当前月份 |
| 右键日历 | 打开外观和行为设置 |
| 点击待办前的彩色圆点 | 设置该待办的高亮颜色 |
| 双击托盘图标 | 恢复隐藏窗口或解除鼠标穿透 |

启用“锁定并允许穿透点击”后，鼠标操作会穿过日历，不影响桌面图标。

## 本地数据

所有日程和软件设置保存在：

```text
%LOCALAPPDATA%\TidesCalendar\data.json
```

软件不会主动上传这些数据。备份待办内容时，只需复制该 `data.json` 文件。

## 项目文件

| 文件 | 说明 |
| --- | --- |
| `TidesCalendar.cs` | WinForms 应用完整源码 |
| `LXGWWenKaiScreen.ttf` | 编译时嵌入的霞鹭文楷字体 |
| `LXGW-WenKai-OFL.txt` | 字体许可证 |
| `潮汐日历.exe` | 已编译的 Windows 可执行文件 |
| `潮汐日历-使用说明.txt` | 简明使用说明 |

## 从源码编译

### 环境要求

- Windows 10 或 Windows 11
- .NET Framework 4.x
- Windows 自带的 C# 编译器 `csc.exe`

在包含源码和字体文件的目录中打开 PowerShell，然后运行：

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
  /nologo `
  /target:winexe `
  /optimize+ `
  /platform:anycpu `
  "/out:潮汐日历.exe" `
  "/resource:.\LXGWWenKaiScreen.ttf,LXGWWenKai.ttf" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  ".\TidesCalendar.cs"
```

编译成功后，当前目录会生成 `潮汐日历.exe`。

## 字体与许可证

本项目使用霞鹭文楷。字体文件遵循 SIL Open Font License 1.1，详情见：

```text
LXGW-WenKai-OFL.txt
```

## 兼容性说明

- 当前版本面向 Windows 桌面环境。
- 程序为原生 WinForms 应用，不是网页或桌面网页容器。
- 可执行文件未进行商业代码签名；建议仅运行自己编译或从可信仓库获取的版本。
