import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:window_manager/window_manager.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:system_tray/system_tray.dart';
import 'package:path_provider/path_provider.dart';
import 'home_page.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  
  await windowManager.ensureInitialized();
  await hotKeyManager.unregisterAll();
  
  // 先初始化窗口
  WindowOptions windowOptions = const WindowOptions(
    size: Size(360, 640),
    minimumSize: Size(300, 400),
    center: true,
    backgroundColor: Colors.transparent, 
    skipTaskbar: false, 
    titleBarStyle: TitleBarStyle.hidden, 
  );
  
  await windowManager.waitUntilReadyToShow(windowOptions, () async {
    await windowManager.show();
    await windowManager.focus();
    await windowManager.setHasShadow(true);
    
    // 初始化托盘
    await _initSystemTray();
  });

  runApp(const MyApp());
}

// 提取图标到临时文件 (macOS 托盘必须使用真实文件路径)
Future<String> _extractIcon() async {
  String assetPath = 'assets/app_icon.png';
  String fileName = 'tray_icon.png';

  try {
    final byteData = await rootBundle.load(assetPath);
    final tempDir = await getTemporaryDirectory();
    final file = File('${tempDir.path}/$fileName');
    await file.writeAsBytes(byteData.buffer.asUint8List());
    return file.path;
  } catch (e) {
    debugPrint("图标提取失败: $e");
    return "";
  }
}

Future<void> _initSystemTray() async {
  final SystemTray systemTray = SystemTray();
  
  String iconPath = await _extractIcon();
  
  await systemTray.initSystemTray(
    title: "", 
    iconPath: iconPath,
    toolTip: "PigPicPot",
  );

  final Menu menu = Menu();
  await menu.buildFrom([
    MenuItemLabel(label: '打开主界面', onClicked: (menuItem) async {
      await windowManager.show();
      await windowManager.focus();
    }),
    MenuItemLabel(label: '退出', onClicked: (menuItem) => exit(0)),
  ]);

  await systemTray.setContextMenu(menu);

  systemTray.registerSystemTrayEventHandler((eventName) async {
    if (eventName == kSystemTrayEventClick) {
      // 左键点击：只显示窗口
      await windowManager.show();
      await windowManager.focus();
    } else if (eventName == kSystemTrayEventRightClick) {
      systemTray.popUpContextMenu();
    }
  });
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
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFFFB7C5), 
          brightness: Brightness.light
        ),
        scaffoldBackgroundColor: Colors.transparent,
        fontFamily: Platform.isMacOS ? '.AppleSystemUIFont' : 'Microsoft YaHei',
      ),
      home: const HomePage(),
    );
  }
}