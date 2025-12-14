import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:super_drag_and_drop/super_drag_and_drop.dart';
import 'package:super_clipboard/super_clipboard.dart';
import 'package:path/path.dart' as p;

/// 一个简单的内存缓存，用于存储GIF的第一帧，避免反复解码导致的闪烁和CPU消耗
class _GifFrameCache {
  static final Map<String, ui.Image> _cache = {};
  static const int _maxSize = 200; // 最大缓存数量

  static ui.Image? get(String path) => _cache[path];

  static void put(String path, ui.Image image) {
    if (_cache.length >= _maxSize) {
      // 简单移除第一个（最旧的）
      _cache.remove(_cache.keys.first);
    }
    _cache[path] = image;
  }
}

class PigDraggableItem extends StatelessWidget {
  final File file;

  const PigDraggableItem({super.key, required this.file});

  // 复制文件到剪贴板
  Future<void> _copyToClipboard(BuildContext context) async {
    final clipboard = SystemClipboard.instance;
    if (clipboard == null) return;

    final item = DataWriterItem();
    item.add(Formats.fileUri(Uri.file(file.path)));
    
    await clipboard.write([item]);

    if (context.mounted) {
      ScaffoldMessenger.of(context).clearSnackBars(); // 清除旧的，防止堆积
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Row(
            children: [
              const Icon(Icons.check_circle_rounded, color: Colors.white, size: 20),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  '已复制: ${p.basenameWithoutExtension(file.path)}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          duration: const Duration(milliseconds: 1000),
          behavior: SnackBarBehavior.floating,
          width: 260,
          backgroundColor: Colors.pinkAccent.withOpacity(0.9), // 可爱的粉色
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return DragItemWidget(
      dragItemProvider: (DragItemRequest request) async {
        final item = DragItem(localData: file.path);
        item.add(Formats.fileUri(Uri.file(file.path)));
        return item;
      },
      allowedOperations: () => [DropOperation.copy],
      dragBuilder: (context, child) {
        return Opacity(
          opacity: 0.85,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Container(
              width: 120,
              height: 120,
              decoration: BoxDecoration(
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withOpacity(0.2),
                    blurRadius: 10,
                  )
                ]
              ),
              child: _PigImage(file: file, fit: BoxFit.cover),
            ),
          ),
        );
      },
      child: Draggable(
        feedback: const SizedBox(),
        // 使用 Stack 确保 InkWell 在图片之上
        child: Container(
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.06),
                blurRadius: 6,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          clipBehavior: Clip.antiAlias,
          child: Stack(
            fit: StackFit.loose,
            children: [
              // 底层：图片
              Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                   _PigImage(
                    file: file,
                    fit: BoxFit.cover,
                  ),
                ],
              ),
              // 顶层：点击反馈层 (完全覆盖图片)
              Positioned.fill(
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    onTap: () => _copyToClipboard(context),
                    highlightColor: Colors.white.withOpacity(0.3), // 点击时高亮
                    splashColor: Colors.pinkAccent.withOpacity(0.2), // 水波纹颜色
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _PigImage extends StatelessWidget {
  final File file;
  final BoxFit fit;
  
  const _PigImage({required this.file, this.fit = BoxFit.cover});

  @override
  Widget build(BuildContext context) {
    final ext = p.extension(file.path).toLowerCase();
    // 明确判定 GIF
    final isGif = ext == '.gif';

    if (!isGif) {
      // 普通图片（jpg, png, webp...）使用标准 Image.file
      return Image.file(
        file,
        key: ValueKey(file.path), // 确保复用正确
        gaplessPlayback: true,    // 防止重绘闪烁
        fit: fit,
        errorBuilder: (ctx, err, stack) => _buildErrorWidget(),
      );
    }
    
    // GIF 图片使用自定义的静态帧加载器
    return _StaticGifImage(
      file: file,
      fit: fit,
      key: ValueKey(file.path),
    );
  }

  Widget _buildErrorWidget() => Container(
      height: 100,
      color: Colors.grey[200],
      child: const Icon(Icons.broken_image, color: Colors.grey),
  );
}

class _StaticGifImage extends StatefulWidget {
  final File file;
  final BoxFit fit;
  const _StaticGifImage({super.key, required this.file, required this.fit});

  @override
  State<_StaticGifImage> createState() => _StaticGifImageState();
}

class _StaticGifImageState extends State<_StaticGifImage> {
  ui.Image? _image;
  bool _hasError = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant _StaticGifImage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.file.path != widget.file.path) {
      _load();
    }
  }
  
  Future<void> _load() async {
    final path = widget.file.path;
    
    // 1. 先查缓存
    final cachedImage = _GifFrameCache.get(path);
    if (cachedImage != null) {
      if (mounted) {
        setState(() {
          _image = cachedImage;
          _hasError = false;
        });
      }
      return;
    }

    // 2. 缓存未命中，开始加载
    // 如果当前已经有图片（比如复用 widget），为了实现 gaplessPlayback 效果，
    // 我们暂时不把 _image 置空，而是等新图加载完再替换。
    // 只在完全没有图的时候（首次加载）才重置状态（其实不需要特意重置，保持 null 即可显示占位）
    
    if (_image == null && mounted) {
      setState(() {
        _hasError = false;
      });
    }

    try {
        final bytes = await widget.file.readAsBytes();
        final codec = await ui.instantiateImageCodec(bytes);
        final frame = await codec.getNextFrame();
        
        // 存入缓存
        _GifFrameCache.put(path, frame.image);

        if (mounted && widget.file.path == path) { // 再次检查 path 确保没被篡改
            setState(() {
                _image = frame.image;
            });
        }
    } catch (e) {
        debugPrint('Error loading GIF frame for $path: $e');
        if (mounted) {
            setState(() {
                _hasError = true;
            });
        }
    }
  }

  @override
  Widget build(BuildContext context) {
      if (_hasError) {
        return Container(
          height: 100,
          color: Colors.grey[200],
          child: const Icon(Icons.broken_image, color: Colors.grey),
        );
      }
      
      // 如果正在加载且没有旧图显示，展示占位
      if (_image == null) {
         return Container(color: Colors.grey[100]); 
      }
      
      return RawImage(
        image: _image,
        fit: widget.fit,
      );
  }
}
