import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'dart:io';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:hotkey_manager/hotkey_manager.dart';

class SettingsPage extends StatefulWidget {
  final VoidCallback? onHotkeySet;
  const SettingsPage({super.key, this.onHotkeySet});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  String _shortcutLabel = '读取中...';
  bool _isRecording = false;
  final FocusNode _focusNode = FocusNode();

  @override
  void initState() {
    super.initState();
    _loadSavedHotkey();
  }

  @override
  void dispose() {
    _focusNode.dispose();
    super.dispose();
  }

  // 加载保存的快捷键用于显示
  Future<void> _loadSavedHotkey() async {
    final prefs = await SharedPreferences.getInstance();
    final label = prefs.getString('hotkey_label');
    setState(() {
      _shortcutLabel = label ?? (Platform.isMacOS ? 'Ctrl + X' : (Platform.isWindows ? 'Win + X' : 'Ctrl + X'));
    });
  }

  // 开始录制
  void _startRecording() {
    setState(() {
      _isRecording = true;
      _shortcutLabel = '请按下快捷键 (Esc 取消)';
    });
    _focusNode.requestFocus();
  }

  // 处理键盘事件
  void _handleKeyEvent(RawKeyEvent event) async {
    if (!_isRecording) return;
    if (event is! RawKeyDownEvent) return; // 只处理按下事件

    final key = event.logicalKey;

    // 如果按下 Esc，取消录制
    if (key == LogicalKeyboardKey.escape) {
      setState(() {
        _isRecording = false;
      });
      _loadSavedHotkey();
      return;
    }

    // 收集按下的修饰键
    List<HotKeyModifier> modifiers = [];
    if (event.isMetaPressed) modifiers.add(HotKeyModifier.meta);
    if (event.isControlPressed) modifiers.add(HotKeyModifier.control);
    if (event.isAltPressed) modifiers.add(HotKeyModifier.alt);
    if (event.isShiftPressed) modifiers.add(HotKeyModifier.shift);

    // 过滤掉修饰键本身（比如只按下了 Cmd，不要触发保存，要等 Cmd + X）
    if (_isModifier(key)) {
      return;
    }

    // 生成人类可读的 Label
    final label = _generateLabel(modifiers, key);

    // 保存配置
    await _saveHotkeyConfig(modifiers, key, label);
  }

  bool _isModifier(LogicalKeyboardKey key) {
    return key == LogicalKeyboardKey.meta ||
           key == LogicalKeyboardKey.metaLeft ||
           key == LogicalKeyboardKey.metaRight ||
           key == LogicalKeyboardKey.control ||
           key == LogicalKeyboardKey.controlLeft ||
           key == LogicalKeyboardKey.controlRight ||
           key == LogicalKeyboardKey.alt ||
           key == LogicalKeyboardKey.altLeft ||
           key == LogicalKeyboardKey.altRight ||
           key == LogicalKeyboardKey.shift ||
           key == LogicalKeyboardKey.shiftLeft ||
           key == LogicalKeyboardKey.shiftRight;
  }

  String _generateLabel(List<HotKeyModifier> modifiers, LogicalKeyboardKey key) {
    List<String> parts = [];
    for (var m in modifiers) {
      if (m == HotKeyModifier.meta) {
        parts.add(Platform.isMacOS ? 'Cmd' : 'Win');
      }
      else if (m == HotKeyModifier.control) parts.add('Ctrl');
      else if (m == HotKeyModifier.alt) parts.add('Alt');
      else if (m == HotKeyModifier.shift) parts.add('Shift');
    }
    parts.add(key.keyLabel); // keyLabel 通常是大写字母
    return parts.join(' + ');
  }

  Future<void> _saveHotkeyConfig(List<HotKeyModifier> modifiers, LogicalKeyboardKey key, String label) async {
    final prefs = await SharedPreferences.getInstance();
    
    // 1. 保存 Label 用于显示
    await prefs.setString('hotkey_label', label);

    // 2. 保存 Key ID (int) 用于逻辑恢复
    await prefs.setInt('hotkey_key_id', key.keyId);

    // 3. 保存 Modifiers (List<String>)
    List<String> modStrings = modifiers.map((m) => m.toString()).toList();
    await prefs.setStringList('hotkey_modifiers', modStrings);

    setState(() {
      _shortcutLabel = label;
      _isRecording = false;
    });

    // 4. 调用回调，立即重新注册快捷键
    widget.onHotkeySet?.call();

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('快捷键已更新为: $label'),
          behavior: SnackBarBehavior.floating,
          width: 250,
          backgroundColor: Colors.pinkAccent,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          duration: const Duration(seconds: 1),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: Container(
          decoration: BoxDecoration(
            color: const Color(0xFFF8F8F8).withOpacity(0.95),
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
                // === 顶部栏 ===
                Container(
                  height: 60,
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  decoration: BoxDecoration(
                    color: Colors.white.withOpacity(0.6),
                    border: Border(bottom: BorderSide(color: Colors.grey.withOpacity(0.1))),
                  ),
                  child: Row(
                    children: [
                      IconButton(
                        icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 18, color: Colors.grey),
                        onPressed: () => Navigator.pop(context, true), // 返回 true 通知刷新
                      ),
                      const SizedBox(width: 8),
                      const Text('设置', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                    ],
                  ),
                ),
                
                // === 内容区域 ===
                Expanded(
                  child: RawKeyboardListener(
                    focusNode: _focusNode,
                    onKey: _handleKeyEvent,
                    child: ListView(
                      padding: const EdgeInsets.all(20),
                      children: [
                        _buildSectionTitle('快捷键'),
                        
                        // 快捷键设置卡片
                        GestureDetector(
                          onTap: _startRecording,
                          child: Container(
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(8),
                              border: _isRecording ? Border.all(color: Colors.pinkAccent, width: 2) : null,
                            ),
                            child: ListTile(
                              title: const Text('显示/隐藏窗口', style: TextStyle(fontSize: 14)),
                              subtitle: Text(
                                _isRecording ? '请按下组合键...' : _shortcutLabel,
                                style: TextStyle(
                                  color: _isRecording ? Colors.pinkAccent : Colors.grey,
                                  fontWeight: _isRecording ? FontWeight.bold : FontWeight.normal,
                                ),
                              ),
                              trailing: _isRecording 
                                ? const SizedBox(
                                    width: 16, height: 16, 
                                    child: CircularProgressIndicator(strokeWidth: 2)
                                  )
                                : const Icon(Icons.keyboard_outlined, color: Colors.grey),
                            ),
                          ),
                        ),
                        
                        const SizedBox(height: 20),
                        _buildSectionTitle('关于'),
                        Container(
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const ListTile(
                            title: Text('版本', style: TextStyle(fontSize: 14)),
                            trailing: Text('v1.0.0', style: TextStyle(color: Colors.grey)),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildSectionTitle(String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10, left: 4),
      child: Text(
        title,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.bold,
          color: Colors.grey,
        ),
      ),
    );
  }
}
