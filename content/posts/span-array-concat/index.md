---
title: "[C#] Span<T>을 이용한 배열 이어붙이기"
slug: span-array-concat
date: 2023-01-13
summary: "Span<T>을 이용해 두 배열을 이어붙이는 확장 메서드를 만든다."
tags: [csharp, span]
draft: false
---

```csharp
internal static class SpanExtension
{
	public static T[] Concat<T>(this ReadOnlySpan<T> span1, ReadOnlySpan<T> span2)
	{
		T[] ret = new T[span1.Length + span2.Length];

		Span<T> span = new(ret);
		span1.CopyTo(span);
		span2.CopyTo(span[span1.Length..]);

		return ret;
	}
}


// Program.cs
ReadOnlySpan<byte> source1 = "Hello, "u8;
ReadOnlySpan<byte> source2 = "World!"u8;

Console.WriteLine(System.Text.Encoding.UTF8.GetString(source1.Concat(source2)));
// output: Hello, World!
```
