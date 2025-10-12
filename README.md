**仓库内容概要**:    
    本仓库旨在收集大量猪图，系本人在早期/近期高强度网上冲浪中大量筛选并存储的猪图，现同步git，猪图大开源^ ^

**文件分类规则**:
    大致按照文件类型区分(动图/静态图)，细分按照文件夹名字逻辑分类，一些特殊的可能会位于多个目录内，后续可能考虑写个索引页面，通过关键词筛选猪图

**文件命名规则**(暂定):
    目前考虑把猪图按照时间排序，命名以zhu开头，后面衔接若干内容词条(数据标注说是),但目前会把猪图全部按时间排序重排为zhu zhu(1) zhu(2) ...
    后续考虑在索引分类网页中加入词条编辑，查看猪图后选择若干已存在猪图词条zhu（haochi， 喝水， 吃面， 怪猎， xx, 游泳, 打滚, asdf, 睡觉 ...）,编辑并保存，然后将会重新修改文件命名，
    在后续的关键词快速检索猪图中可能考虑采用名称中的关键词进行筛选排序

---

# PigPicPot 查看器 (PigPicPot Viewer)

## 程序细节

*   基于 **C#** 和 **WPF** 构建。
*   本质上是一个图片查看器，用于浏览 `resource` 文件夹内的各种猪猪图。您可以随意向该文件夹内加入或移除图片。
*   由于GIF加载机制复杂，在快速滚动以及展示大量GIF时可能会有一些性能问题。
*   **左键点击** 直接将目标图片复制进入剪贴板。
*   **右键点击** 展开右键菜单，支持以文件形式复制或者导出图片。
*   **自适应标签筛选**: 可以根据标签筛选图片。对于文件名相似、仅以数字编号区分的系列图片，只要遵循命名规则 `pig_english_name_中文名123.后缀名`，程序即可为其自动创建和扩展筛选标签。
*   **GIF懒加载策略**: 为了优化性能，程序对GIF图片采用了懒加载策略（仅在鼠标悬停时加载完整动画，光标未悬停时显示缩略的动画）。但在可预见的未来，如果图片数量激增，此方案可能仍会遇到性能瓶颈。同时展示超多个GIF在加载初期几乎必定引发卡顿。
*   **小窗口**: 按左Ctrl+左Alt+B可呼出一个小窗口，具有本程序大部分功能，永远保持在最前端。热键设置可在配置文件中修改。

---

## Program Details

*   Built with **C#** and **WPF**.
*   Essentially an image viewer for browsing various pig pictures in the `resource` folder. You can freely add or remove pictures from this folder.
*   Due to the complex GIF loading mechanism, there may be some performance issues when scrolling quickly and when displaying a large number of GIFs.
*   **Left-clicking** directly copies the target image to the clipboard.
*   **Right-clicking** brings up a context menu, supporting copying or exporting the image as a file.
*   **Adaptive Tag Filtering**: Allows filtering images by tags. For series of images with similar filenames distinguished only by a number suffix, the program automatically creates and extends filter tags, provided the naming convention `pig_english_name_中文名123.ext` is followed.
*   **GIF Lazy Loading Strategy**: To optimize performance, the program uses a lazy loading strategy for GIFs (The full animation is loaded only on mouse hover; when the cursor is not hovering, a thumbnail animation is displayed.). However, in the foreseeable future, this solution may still encounter performance bottlenecks if the number of images increases dramatically. Displaying a very large number of GIFs will almost certainly cause lag during the initial loading phase.
*   **Mini Window**: Press Left Ctrl + Left Alt + B to bring up a mini window that has most of the program's features and always stays on top. Hotkey settings can be modified in the configuration file.

## 主要贡献者

*   **图片收集**: SCP5637
*   **整理与代码**: JodieRuth

## Main Contributors

*   **Image Collection**: SCP5637
*   **Organization & Code**: JodieRuth

---
由Google翻译提供本项目的英文本地化与本文档的英文翻译。
English localization support & English translation of this document are provided by Google Translate.