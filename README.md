# PigPicPot 查看器 (PigPicPot Viewer)

**仓库内容概要**: 本仓库旨在收集大量猪图，系本人在早期/近期高强度网上冲浪中大量筛选并存储的猪图，现同步git，猪图大开源^ ^

---

## 简体中文

### 功能

*   **图片浏览**: 一个为浏览`resource`文件夹内图片而生的WPF应用程序。您可以随意向该文件夹内添加或移除图片和子文件夹。
*   **动态标签系统**: 程序会自动扫描`resource`文件夹下的所有目录与子目录，并为它们生成层级标签。有多少层子目录，就有多少级标签。
*   **智能图片识别**: 程序只会在最深层的子目录中扫描并加载图片，这使得您可以将非内容的图片（如封面图）放在上层目录中，程序会自动忽略它们。
*   **文件名标签**: 在最深层的子目录中，程序会根据图片文件名动态生成最后一级标签，方便对系列图片进行筛选。你的图片文件名需要像这样：`pig_任何可能的英文本地化名_任何可能的中文本地化名.后缀名`。如果你需要为多张类似图片生成一个动态的标签，你可以从第二张开始命名为`pig_任何可能的英文本地化名2_任何可能的中文本地化名2.后缀名`。此时程序会为这些图片创建一个属于它们的自定义动态标签，根据你的文件名而定。
*   **一键复制**: 左键点击任意图片，即可将其（包括动态GIF）复制到剪贴板。
*   **迷你模式**: 按下热键 `LeftCtrl+LeftAlt+B` 可以呼出一个永远置顶的迷你窗口，它包含了主窗口的大部分功能，方便您在处理其他事务时快速取用图片。迷你窗口会在复制任何一张图片之后自行关闭，点击左上角的图钉图标可以固定以让它不再自动关闭。
*   **GIF 修复与优化**: 内置了强大的`ImageSharp`图像处理库。在首次加载时，程序会自动检测并尝试修复所有GIF文件。对于轻微损坏或格式不规范的GIF，程序会将其重新编码为标准格式并替换原文件；对于严重损坏的文件，则会安全地跳过，从而根除了因坏图导致程序崩溃的问题。
*   **收藏夹功能**: 可以创建和管理多个收藏夹，将喜欢的图片添加到不同的收藏夹中，方便快速访问。
*   **高度可配置**: 您可以通过修改程序目录下的`usersettings.json`文件来自定义主窗口与迷你窗口的背景图、窗口大小、以及迷你模式的快捷键。

### 如何使用

1.  将您自己的图片或文件夹放入程序目录下的`resource`文件夹。
2.  运行`run.bat`启动程序。程序会自动为您生成图片标签。
3.  点击各级标签进行筛选，在右侧找到您想要的图片。
4.  左键点击图片即可复制。
5.  可以使用收藏夹功能保存喜欢的图片，通过右键菜单添加图片到收藏夹。
6.  按下 `LeftCtrl+LeftAlt+B` 可以打开迷你模式窗口。

### 构建和发布

1.  运行 `build.bat` 脚本以构建发布版本。
2.  构建完成后，发布文件将位于 `Release` 文件夹中。

**注意**: 由于程序会在首次启动时加载并处理所有GIF文件，如果您的GIF图片非常多，在首次启动时可能会需要等待一小会儿才能看到所有GIF的缩略图，这是正常现象。

---

## English

### Features

*   **Image Browsing**: A WPF application designed for browsing images within the `resource` folder. You can freely add or remove images and subfolders.
*   **Dynamic Tag System**: The program automatically scans all directories and subdirectories under `resource` and generates hierarchical tags for them. The number of subdirectory levels determines the number of tag levels.
*   **Smart Image Recognition**: The program only scans for and loads images in the deepest subdirectories, allowing you to place non-content images (like cover art) in upper-level directories, which will be automatically ignored.
*   **Filename-Based Tags**: In the deepest subdirectories, the program dynamically generates the final level of tags based on image filenames, making it easy to filter image series. Your image filenames should follow this pattern: `pig_any_english_name_any_chinese_name.extension`. If you want to generate a dynamic tag for a series of similar images, you can name subsequent images like `pig_any_english_name2_any_chinese_name2.extension`. The program will then create a custom dynamic tag for these images based on their shared filename components.
*   **One-Click Copy**: Left-click on any image (including animated GIFs) to copy it directly to your clipboard.
*   **Mini-Mode**: Press the hotkey `LeftCtrl+LeftAlt+B` to summon an always-on-top mini-window that includes most of the main window's functionality, allowing for quick access to images while working on other tasks. The mini-window will automatically close after copying an image. Click the pin icon in the top-left corner to keep it open.
*   **GIF Repair & Optimization**: Built with the powerful `ImageSharp` library. On first launch, the application automatically detects and attempts to repair all GIF files. It re-encodes non-standard or slightly corrupted GIFs into a standard format (overwriting the original file) and safely skips severely damaged ones, completely eliminating crashes caused by bad images.
*   **Favorites**: Create and manage multiple favorites folders to save your preferred images for quick access.
*   **Highly Configurable**: You can customize the background images, window sizes for both the main and mini-windows, and the mini-mode hotkey by editing the `usersettings.json` file in the program's directory.

### How to Use

1.  Place your own images or folders into the `resource` folder located in the program's directory.
2.  Run `run.bat` to start the application. The program will automatically generate tags for your images.
3.  Click on the tags at various levels to filter and find the image you want on the right.
4.  Left-click an image to copy it.
5.  Use the favorites feature to save your preferred images by right-clicking on images and adding them to favorites.
6.  Press `LeftCtrl+LeftAlt+B` to open the mini-mode window.

### Building and Deployment

1.  Run the `build.bat` script to build the release version.
2.  After building, the release files will be located in the `Release` folder.

**Note**: Because the program processes and potentially repairs all GIF files on its first run, you may experience a short delay before all GIF thumbnails appear if you have a very large number of them. This is normal behavior.

---

## 主要贡献者 (Main Contributors)

*   **图片收集 (Image Collection)**: SCP5637
*   **整理与代码 (Organization & Code)**: JodieRuth

---

由Google翻译提供本项目的英文本地化与本文档的英文翻译。
请随时告知可能存在的各种bug。我的编程水平并不优秀，我会尽我所能解决它，如果你自己能动手解决欢迎PR。

English localization support & English translation of this document are provided by Google Translate.
Please feel free to report any bugs you may find. My programming skills are not the best, but I will do my best to fix them. If you can fix them yourself, pull requests are welcome.