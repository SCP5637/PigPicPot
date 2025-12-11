import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:file_selector/file_selector.dart';
import 'package:flutter_staggered_grid_view/flutter_staggered_grid_view.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:path/path.dart' as p;
import 'package:window_manager/window_manager.dart';
import 'pig_draggable.dart';
import 'dart:ui'; // 用于 ImageFilter

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> with WindowListener {
  // 数据源
  List<File> _allPigFiles = [];
  List<File> _displayedPigFiles = [];
  
  // 状态
  bool _isAlwaysOnTop = false;
  String? _rootPath;
  bool _isLoading = false;
  final TextEditingController _searchController = TextEditingController();
  final FocusNode _searchFocusNode = FocusNode();

  @override
  void initState() {
    super.initState();
    windowManager.addListener(this);
    _registerHotkey();
    // 实际使用时，建议保存路径到 shared_preferences，这里先留空
  }

  @override
  void dispose() {
    windowManager.removeListener(this);
    hotKeyManager.unregisterAll();
    _searchController.dispose();
    _searchFocusNode.dispose();
    super.dispose();
  }

  // 注册全局快捷键
  Future<void> _registerHotkey() async {
    // 根据平台定义快捷键
    HotKey hotKey;
    if (Platform.isMacOS) {
      // macOS: Control + X
      hotKey = HotKey(
        KeyCode.keyX,
        modifiers: [KeyModifier.control],
        scope: HotKeyScope.system, 
      );
    } else {
      // Windows: Win + X (注意：Win+X 是系统快捷键，可能会有冲突，建议使用 Alt+X 作为备选)
      // 这里尝试注册 Meta + X
      hotKey = HotKey(
        KeyCode.keyX,
        modifiers: [KeyModifier.meta], 
        scope: HotKeyScope.system,
      );
    }

    await hotKeyManager.register(
      hotKey,
      keyDownHandler: (hotKey) async {
        if (await windowManager.isVisible()) {
          if (await windowManager.isFocused()) {
            await windowManager.hide();
          } else {
            await windowManager.show();
            await windowManager.focus();
          }
        } else {
          await windowManager.show();
          await windowManager.focus();
        }
      },
    );
  }

  Future<void> _pickDirectory() async {
    final String? directoryPath = await getDirectoryPath(
      confirmButtonText: '选择 PigPicPot 根目录',
    );

    if (directoryPath != null) {
      setState(() {
        _rootPath = directoryPath;
        _isLoading = true;
      });
      await _scanImages(directoryPath);
    }
  }

  Future<void> _scanImages(String rootDir) async {
    List<File> files = [];
    final dirsToScan = [
      Directory(p.join(rootDir, 'gif')),
      Directory(p.join(rootDir, 'pic')),
    ];

    for (var dir in dirsToScan) {
      if (await dir.exists()) {
        await for (var entity in dir.list(recursive: true, followLinks: false)) {
          if (entity is File) {
            final ext = p.extension(entity.path).toLowerCase();
            if (['.gif', '.jpg', '.jpeg', '.png', '.webp'].contains(ext)) {
              files.add(entity);
            }
          }
        }
      }
    }

    // 按名称排序
    files.sort((a, b) => p.basename(a.path).compareTo(p.basename(b.path)));

    if (mounted) {
      setState(() {
        _allPigFiles = files;
        _displayedPigFiles = files;
        _isLoading = false;
      });
    }
  }

  void _filterImages(String query) {
    if (query.isEmpty) {
      setState(() {
        _displayedPigFiles = _allPigFiles;
      });
    } else {
      setState(() {
        _displayedPigFiles = _allPigFiles.where((file) {
          return p.basename(file.path).toLowerCase().contains(query.toLowerCase());
        }).toList();
      });
    }
  }

  Future<void> _toggleAlwaysOnTop() async {
    setState(() {
      _isAlwaysOnTop = !_isAlwaysOnTop;
    });
    await windowManager.setAlwaysOnTop(_isAlwaysOnTop);
  }

  @override
  Widget build(BuildContext context) {
    // 整体背景容器，带圆角和模糊
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFF8F8F8).withOpacity(0.95), // 微微透的背景
        borderRadius: BorderRadius.circular(12),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.15),
            blurRadius: 20,
            spreadRadius: 4,
          ),
        ],
        border: Border.all(color: Colors.white.withOpacity(0.5), width: 1.5),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(12),
        child: Column(
          children: [
            // === 自定义顶部栏 (Drag Handle + Search) ===
            GestureDetector(
              onPanStart: (details) => windowManager.startDragging(),
              child: Container(
                height: 60,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                decoration: BoxDecoration(
                  color: Colors.white.withOpacity(0.6),
                  border: Border(bottom: BorderSide(color: Colors.grey.withOpacity(0.1))),
                ),
                child: Row(
                  children: [
                    // 文件夹选择
                    IconButton(
                      icon: const Icon(Icons.folder_open_rounded, color: Colors.grey),
                      onPressed: _pickDirectory,
                      tooltip: "选择图库文件夹",
                    ),
                    // 搜索框
                    Expanded(
                      child: Container(
                        height: 36,
                        margin: const EdgeInsets.symmetric(horizontal: 8),
                        decoration: BoxDecoration(
                          color: Colors.grey.withOpacity(0.1),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: TextField(
                          controller: _searchController,
                          focusNode: _searchFocusNode,
                          onChanged: _filterImages,
                          decoration: const InputDecoration(
                            hintText: '搜点什么猪...',
                            hintStyle: TextStyle(fontSize: 13, color: Colors.grey),
                            border: InputBorder.none,
                            contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                            prefixIcon: Icon(Icons.search, size: 18, color: Colors.grey),
                          ),
                          style: const TextStyle(fontSize: 13),
                        ),
                      ),
                    ),
                    // 置顶按钮
                    IconButton(
                      icon: Icon(
                        _isAlwaysOnTop ? Icons.push_pin_rounded : Icons.push_pin_outlined,
                        color: _isAlwaysOnTop ? Colors.pinkAccent : Colors.grey,
                      ),
                      onPressed: _toggleAlwaysOnTop,
                      tooltip: _isAlwaysOnTop ? "取消置顶" : "置顶窗口",
                    ),
                    // 关闭按钮 (可选，因为我们有快捷键隐藏，但还是给个退出的路)
                    IconButton(
                      icon: const Icon(Icons.close_rounded, color: Colors.grey),
                      onPressed: () => windowManager.hide(),
                    ),
                  ],
                ),
              ),
            ),
            
            // === 内容区域 ===
            Expanded(
              child: _buildBody(),
            ),
            
            // === 底部简易状态栏 ===
            Container(
              height: 24,
              alignment: Alignment.center,
              child: Text(
                '共 ${_displayedPigFiles.length} 只猪猪',
                style: TextStyle(color: Colors.grey[400], fontSize: 10),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_rootPath == null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.image_search_rounded, size: 64, color: Colors.pink[100]),
            const SizedBox(height: 16),
            Text(
              '请先投喂猪图文件夹',
              style: TextStyle(color: Colors.grey[600]),
            ),
          ],
        ),
      );
    }

    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_displayedPigFiles.isEmpty) {
      return Center(
        child: Text('没有找到这只猪...', style: TextStyle(color: Colors.grey[400])),
      );
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: MasonryGridView.count(
        crossAxisCount: 3,
        mainAxisSpacing: 10,
        crossAxisSpacing: 10,
        itemCount: _displayedPigFiles.length,
        itemBuilder: (context, index) {
          final file = _displayedPigFiles[index];
          return PigDraggableItem(file: file);
        },
      ),
    );
  }
}