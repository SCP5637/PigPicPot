import 'dart:io';
import 'package:flutter/material.dart';
import 'package:super_drag_and_drop/super_drag_and_drop.dart';
import 'package:path/path.dart' as p;

class PigDraggableItem extends StatelessWidget {
  final File file;

  const PigDraggableItem({super.key, required this.file});

  @override
  Widget build(BuildContext context) {
    return DragItemWidget(
      dragItemProvider: (DragItemRequest request) async {
        final item = DragItem(localData: file.path);
        // 关键：为了兼容微信/QQ发送，必须添加 fileUri
        item.add(Formats.fileUri(Uri.file(file.path)));
        return item;
      },
      allowedOperations: () => [DropOperation.copy],
      dragBuilder: (context, child) {
        // 拖拽时的样式：半透明的小图
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
        feedback: const SizedBox(), // 使用 dragBuilder 接管反馈
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
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: Image.file(
                  file,
                  fit: BoxFit.cover,
                  errorBuilder: (ctx, err, stack) => Container(
                    color: Colors.grey[200],
                    child: const Icon(Icons.broken_image, color: Colors.grey),
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