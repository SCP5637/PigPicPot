import 'dart:io';
import 'package:flutter/material.dart';
import 'package:window_manager/window_manager.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'home_page.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  
  // 初始化窗口管理
  await windowManager.ensureInitialized();
  // 初始化热键管理
  await hotKeyManager.unregisterAll();

  WindowOptions windowOptions = const WindowOptions(
    size: Size(360, 640), // 调整为更修长的手机比例
    minimumSize: Size(300, 400),
    center: true,
    backgroundColor: Colors.transparent, // 关键：背景透明，交由 Flutter 绘制
    skipTaskbar: false,
    titleBarStyle: TitleBarStyle.hidden, // 隐藏系统标题栏
  );
  
  await windowManager.waitUntilReadyToShow(windowOptions, () async {
    await windowManager.show();
    await windowManager.focus();
    await windowManager.setHasShadow(true);
  });

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'PigPicPot',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        // 使用更柔和的粉色作为主题色
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFFFB7C5), 
          brightness: Brightness.light
        ),
        scaffoldBackgroundColor: Colors.transparent, // 配合窗口透明
        fontFamily: Platform.isMacOS ? '.AppleSystemUIFont' : 'Microsoft YaHei',
      ),
      home: const HomePage(),
    );
  }
}