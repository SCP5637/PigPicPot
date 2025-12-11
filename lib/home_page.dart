import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:file_selector/file_selector.dart';
import 'package:flutter_staggered_grid_view/flutter_staggered_grid_view.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:path/path.dart' as p;
import 'package:window_manager/window_manager.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'pig_draggable.dart';
import 'settings_page.dart'; // 引入设置页
import 'dart:ui'; 

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
  
  // 防抖锁
  bool _isProcessingHotkey = false;
  
  // 信号通知器
  final ValueNotifier<int> _hotkeySignal = ValueNotifier(0);

  // 存储 Key
  static const String _prefKeyRootPath = 'pig_root_path';

  @override
  void initState() {
    super.initState();
    windowManager.addListener(this);
    
    // 监听信号，在主线程安全操作窗口
    _hotkeySignal.addListener(() {
      if (_isProcessingHotkey) return;
      _isProcessingHotkey = true;
      
      debugPrint("收到快捷键信号，执行操作");
      
      Future.delayed(const Duration(milliseconds: 100), () async {
        try {
          if (await windowManager.isVisible()) {
            await windowManager.hide();
          } else {
            await windowManager.show();
            await windowManager.focus();
            await windowManager.restore();
          }
        } catch (e) {
          debugPrint("窗口操作异常: $e");
        } finally {
           _isProcessingHotkey = false;
        }
      });
    });

    _registerHotkey(); // 全平台注册快捷键
    _loadSavedPath(); // 启动时加载路径
  }

  @override
  void dispose() {
    windowManager.removeListener(this);
    hotKeyManager.unregisterAll();
    _searchController.dispose();
    _searchFocusNode.dispose();
    _hotkeySignal.dispose(); // 释放
    super.dispose();
  }

  // 加载保存的路径
  Future<void> _loadSavedPath() async {
    final prefs = await SharedPreferences.getInstance();
    final savedPath = prefs.getString(_prefKeyRootPath);
    if (savedPath != null) {
      final dir = Directory(savedPath);
      if (await dir.exists()) {
        setState(() {
          _rootPath = savedPath;
          _isLoading = true;
        });
        await _scanImages(savedPath);
      }
    }
  }

  // 注册全局快捷键
  Future<void> _registerHotkey() async {
    debugPrint("正在注册全局快捷键...");
    
    final prefs = await SharedPreferences.getInstance();
    
    // 默认快捷键: Mac为Cmd+X, 其他为Ctrl+X
    int? keyId = prefs.getInt('hotkey_key_id');
    List<String>? modifiersStr = prefs.getStringList('hotkey_modifiers');
    
    debugPrint("读取配置: keyId=$keyId, modifiers=$modifiersStr");

    LogicalKeyboardKey key;
    List<HotKeyModifier> modifiers;

    if (keyId != null && modifiersStr != null) {
      key = LogicalKeyboardKey.findKeyByKeyId(keyId) ?? LogicalKeyboardKey.keyX;
      debugPrint("恢复按键: ID=$keyId -> ${key.debugName}");
      
      modifiers = modifiersStr.map((e) {
        if (e.contains('meta')) return HotKeyModifier.meta;
        if (e.contains('control')) return HotKeyModifier.control;
        if (e.contains('alt')) return HotKeyModifier.alt;
        if (e.contains('shift')) return HotKeyModifier.shift;
        return HotKeyModifier.meta;
      }).toList();
    } else {
      key = LogicalKeyboardKey.keyX;
      // 用户需求: Mac -> Ctrl+X, Windows -> Win+X
      if (Platform.isMacOS) {
        modifiers = [HotKeyModifier.control];
      } else if (Platform.isWindows) {
        modifiers = [HotKeyModifier.meta];
      } else {
        modifiers = [HotKeyModifier.control];
      }
    }

    final hotKey = HotKey(
      key: key,
      modifiers: modifiers,
      scope: HotKeyScope.system, 
    );

    await hotKeyManager.unregisterAll();

    try {
      // 这里的 lastTriggerTime 用于防止物理按键长按导致的事件洪流
      DateTime lastTriggerTime = DateTime.fromMillisecondsSinceEpoch(0);
      
      await hotKeyManager.register(
        hotKey,
        keyDownHandler: (hotKey) {
          final now = DateTime.now();
          // 限制每 500ms 只能触发一次
          if (now.difference(lastTriggerTime) > const Duration(milliseconds: 500)) {
            lastTriggerTime = now;
            debugPrint("触发快捷键: ${key.keyId}");
            _hotkeySignal.value++; 
          }
        },
      );
      debugPrint("快捷键注册成功: $modifiers + $key");
    } catch (e) {
      debugPrint("快捷键注册失败: $e");
    }
  }  Future<void> _pickDirectory() async {
    final String? directoryPath = await getDirectoryPath(
      confirmButtonText: '选择 PigPicPot 根目录',
    );

    if (directoryPath != null) {
      // 保存路径
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_prefKeyRootPath, directoryPath);

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
    // 整体背景容器
    return Material(
      color: Colors.transparent,
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Container(
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
                // === 自定义顶部栏 ===
                Container(
                  height: 60, // 缩短高度
                  padding: const EdgeInsets.only(top: 10), // 缩短内边距
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.6),
                    border: Border(bottom: BorderSide(color: Colors.grey.withOpacity(0.1))),
                  ),
                  child: Stack(
                    children: [
                      // 1. 底层：拖拽监听层
                      GestureDetector(
                        behavior: HitTestBehavior.translucent,
                        onPanStart: (details) => windowManager.startDragging(),
                        child: const SizedBox.expand(),
                      ),
                      // 2. 顶层：交互按钮层
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        child: Row(
                          children: [
                            // 文件夹选择
                            IconButton(
                              icon: const Icon(Icons.folder_open_rounded, color: Colors.grey),
                              onPressed: _pickDirectory,
                              tooltip: "更换图库文件夹",
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
                            // 设置按钮
                            IconButton(
                              icon: const Icon(Icons.settings_rounded, color: Colors.grey),
                              onPressed: () {
                                Navigator.push(
                                  context,
                                  PageRouteBuilder(
                                    pageBuilder: (context, animation, secondaryAnimation) => const SettingsPage(),
                                    transitionsBuilder: (context, animation, secondaryAnimation, child) {
                                      const begin = 0.0;
                                      const end = 1.0;
                                      final tween = Tween(begin: begin, end: end);
                                      final fadeAnimation = animation.drive(tween);
                                      return FadeTransition(
                                        opacity: fadeAnimation,
                                        child: child,
                                      );
                                    },
                                    transitionDuration: const Duration(milliseconds: 200), // 缩短动画时间，更干脆
                                  ),
                                ).then((_) => _registerHotkey()); // 返回时刷新快捷键
                              },
                              tooltip: "设置",
                            ),
                            // 关闭按钮
                            IconButton(
                              icon: const Icon(Icons.close_rounded, color: Colors.grey),
                              onPressed: () => windowManager.hide(),
                            ),
                          ],
                        ),
                      ),
                    ],
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
