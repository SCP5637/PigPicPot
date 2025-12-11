import 'dart:io';
import 'package:flutter/material.dart';
import 'package:super_drag_and_drop/super_drag_and_drop.dart';
import 'package:super_clipboard/super_clipboard.dart';
import 'package:path/path.dart' as p;

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
              child: Image.file(file, fit: BoxFit.cover),
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
                   Image.file(
                    file,
                    key: ValueKey(file.path), // 增加 Key 帮助复用
                    gaplessPlayback: true, // 防止图片重绘时闪烁
                    fit: BoxFit.cover,
                    errorBuilder: (ctx, err, stack) => Container(
                      height: 100,
                      color: Colors.grey[200],
                      child: const Icon(Icons.broken_image, color: Colors.grey),
                    ),
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