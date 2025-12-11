import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:window_manager/window_manager.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:system_tray/system_tray.dart';
import 'package:path_provider/path_provider.dart';
import 'package:path/path.dart' as p;
import 'home_page.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await windowManager.ensureInitialized();
  await hotKeyManager.unregisterAll();

  // 获取当前主题亮度 (保留原有托盘图标逻辑)
  final brightness = WidgetsBinding.instance.platformDispatcher.platformBrightness;

  WindowOptions windowOptions = const WindowOptions(
    size: Size(360, 640), // 恢复原始窗口大小
    minimumSize: Size(300, 400), // 恢复原始最小窗口大小
    center: true,
    backgroundColor: Colors.transparent,
    skipTaskbar: true, // 恢复原始跳过任务栏设置
    titleBarStyle: TitleBarStyle.hidden, // 这会隐藏标题栏
    windowButtonVisibility: false, // 隐藏红绿灯按钮
  );

  windowManager.waitUntilReadyToShow(windowOptions, () async {
    await windowManager.show();
    await windowManager.focus();
    await windowManager.setHasShadow(true); // 恢复原始阴影设置

    // 初始化托盘 (保留原有托盘图标逻辑)
    await _initSystemTray(brightness);
  });

  runApp(const MyApp());
}

// 提取图标到临时文件 (保留原有托盘图标逻辑)
Future<String> _extractIcon(Brightness brightness) async {
  String assetPath;
  if (Platform.isWindows) {
    assetPath = (brightness == Brightness.light)
        ? 'assets/app_icon_light.ico'
        : 'assets/app_icon.ico';
  } else {
    assetPath = 'assets/app_icon.png';
  }

  final String fileName = Platform.isWindows ? 'tray_icon.ico' : 'tray_icon.png';

  try {
    final byteData = await rootBundle.load(assetPath);
    final tempDir = await getTemporaryDirectory();
    final file = File(p.join(tempDir.path, fileName));
    await file.writeAsBytes(byteData.buffer.asUint8List());
    debugPrint("图标已成功提取到: ${file.path}");
    return file.path;
  } catch (e) {
    debugPrint("提取托盘图标失败: $e");
    if (Platform.isWindows && brightness == Brightness.light) {
      try {
        final byteData = await rootBundle.load('assets/app_icon.ico');
        final tempDir = await getTemporaryDirectory();
        final file = File(p.join(tempDir.path, 'tray_icon.ico'));
        await file.writeAsBytes(byteData.buffer.asUint8List());
        debugPrint("回退到默认图标: ${file.path}");
        return file.path;
      } catch (fallbackError) {
        debugPrint("回退图标提取也失败: $fallbackError");
        return "";
      }
    }
    return "";
  }
}

bool _systemTrayInitialized = false;

Future<void> _initSystemTray(Brightness brightness) async {
  if (_systemTrayInitialized) {
    debugPrint("系统托盘已初始化，跳过。");
    return;
  }

  String iconPath = await _extractIcon(brightness);

  if (iconPath.isEmpty) {
    debugPrint("图标路径为空，中止系统托盘初始化。");
    return;
  }

  final SystemTray systemTray = SystemTray();

  try {
    await systemTray.initSystemTray(
      title: "",
      iconPath: iconPath,
      toolTip: "PigPicPot",
    );
  } catch (e) {
    debugPrint("初始化系统托盘失败: $e");
    return;
  }

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
      await windowManager.show();
      await windowManager.focus();
    } else if (eventName == kSystemTrayEventRightClick) {
      systemTray.popUpContextMenu();
    }
  });

  _systemTrayInitialized = true;
  debugPrint("系统托盘初始化成功。");
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