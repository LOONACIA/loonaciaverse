---
title: "ReadOnlySpan<char> Split하기"
slug: readonlyspan-char-split
date: 2023-01-15
summary: "닷넷 7에는 없는 ReadOnlySpan<char>의 Split을 덕 타이핑 열거자로 직접 구현해 본다."
tags: [csharp, span]
draft: false
---

닷넷 7 기준 Span과 ReadOnlySpan에는 아직 Split이 없다. 닷넷 6에서 Span과 Memory에 온갖 기능이 추가되었고 그 외 내부 API도 차근차근 Span과 ReadOnlySpan으로 바뀌어가는 와중에, 꽤 자주 쓸 법한 Split이 추가되지 않은 것은 꽤나 의외이다.

만약 Split을 하는 목적이 Parse나 문자열 확인 등이라면 string.Split을 쓰는 것보다는 ReadOnlySpan을 이용하는 것이 불필요한 힙 할당을 줄이는 것에 도움이 될 수 있다.

사실 Span\<T\>와 ReadOnlySpan\<T\>에는 이미 특정 값과 일치하는 첫 번째 인덱스를 반환하는 IndexOf\<T\>라는 확장 메서드가 구현되어 있어서, 마음먹고 구현하고자 한다면 간단한 수준의 Split 구현은 그리 어려운 일이 아니다.

그래서 이번 게시물에서는 string 객체의 뷰인 ReadOnlySpan\<char\>를 이용해 문자열을 Split하는 기능을 ReadOnlySpan\<char\>의 확장 메서드 형태로 구현해 보려고 한다.

일반적으로 Split을 하게 된다면 Split된 문자열을 순회하면서 특정한 작업을 하는 것이 주 목적이 되므로, Enumerator를 구현해 foreach 문에서 반복할 수 있도록 구현해 보자.

또한 열거자에서 매번 string 객체를 생성해 반환할 거면 string의 Split 메서드를 쓰지 굳이 ReadOnlySpan을 쓸 이유가 없으므로, 가능하면 매번 string 객체를 생성해 제공하는 대신 Split된 문자열에 대한 뷰만 ReadOnlySpan\<char\> 타입으로 제공해 주는 쪽으로 구현해 보도록 하자.

다만 Enumerator를 본격적으로 구현하기 전에, 먼저 ReadOnlySpan이 ref struct라는 것이 아주 약간의 걸림돌이 된다는 점을 짚고 넘어가야만 하겠다.

> 왜 걸림돌이 되는데?

그걸 알기 위해서는 IEnumerable과 IEnumerator, 그리고 ref struct에 대해서 아주 간략하게나마 이해할 필요가 있다.

특정한 컬렉션을 foreach 문 등을 통해 반복하도록 하기 위해서는 IEnumerable 또는 IEnumerable\<T\> 인터페이스를 구현함으로써 이루는 것이 일반적이다.

한 번 IEnumerable.cs의 소스 코드를 살펴보자.

```csharp
public interface IEnumerable
{
	IEnumerator GetEnumerator();
}
```

보다시피 IEnumerable 인터페이스는 단순히 IEnumerator를 반환하는 GetEnumerator 메서드를 갖는 간단한 인터페이스이다. 이를 제너릭으로 재구현하는 IEnumerable\<T\> 인터페이스도, GetEnumerator 메서드의 반환값을 IEnumerator\<T\> 타입으로 재정의하는 부분 외에 특이사항이 없다.

```csharp
public interface IEnumerator
{
	object Current { get; }

	bool MoveNext();

	void Reset();
}
```

IEnumerator 역시 특별한 부분은 없다. IEnumerator\<T\>는 Current 프로퍼티의 타입이 T로 바뀌는 것뿐, 다른 것은 모두 IEnumerator와 동일하다.

foreach문을 통해 특정한 컬렉션의 열거자를 호출하면 내부적으로는 GetEnumerator 메서드를 호출해 IEnumerator 타입의 열거자를 가져오고, 열거자의 MoveNext 메서드를 호출해 반복 가능 여부를 확인하고, Current 프로퍼티의 getter를 통해 컬렉션 내부의 값을 가져오는 식으로 동작한다.

그럼 우리가 핸들링할 타입은 ReadOnlySpan\<char\>니까 IEnumerable\<ReadOnlySpan\<char\>\> 인터페이스를 구현하면 되는 거 아닌가? 싶지만 안타깝게도 그렇게 속편하게 구현되진 않는다.

문제는 ReadOnlySpan의 유형이 ref struct라는데 있다.

ref struct는 반드시 스택에만 존재할 수 있으며, 힙에 할당될 수 없다. 그래서 박싱의 여지가 있는 모든 동작이 차단되며, 일반적으로 클래스나 구조체, 또는 레코드의 필드일 수 없다. ref struct는 오직, 똑같이 스택에만 존재할 수 있는 ref struct에서만 필드로 선언될 수 있다. 또한 ref struct는 인터페이스를 구현할 수도 없고, 구조체이므로 특정 클래스를 상속할 수도 없다.

그럼 ReadOnlySpan\<char\> 타입으로는 열거자 구현이 불가능한가...? 하면 그건 아니다.

다행히도 C#은 덕 타이핑을 지원하기 때문에, IEnumerable 인터페이스와 IEnumerator 인터페이스에서 정의하는 사양을 충족하기만 한다면 ref struct 타입도 열거자를 제공해 foreach 문을 돌리는 것이 가능하다. ref sturct 뿐만 아니라 클래스나 구조체, 레코드도 그렇다.

따라서 GetEnumerator 메서드가 존재하고, GetEnumerator 메서드를 통해 반환받는 객체 내부에 Current 프로퍼티, MoveNext 메서드가 존재한다면 굳이 IEnumerable 인터페이스를 구현하지 않아도 foreach 문을 돌릴 수 있는 것이다.

그래서 Span이나 ReadOnlySpan을 이용해 열거자를 제공하는 닷넷의 기능들 (예를 들어 ReadOnlySpan\<char\>에 대 Regex라든지)은 전부 덕 타이핑된 열거자(주로 ref struct 자신이 된다)를 반환하는 식으로 구현되어 있다.

그럼 열거자는 덕 타이핑으로 구현하는 것으로 하고... Split된 문자열을 ReadOnlySpan\<char\> 타입으로 제공하는 것을 베이스로 정의 부분만 뽑아보면 다음과 같이 만들어 볼 수 있을 것이다.

```csharp
public ref struct SpanSplitEnumerator
{
	// 원본 문자열
	private ReadOnlySpan<char> _string;

	// 구분자
	private readonly ReadOnlySpan<char> _separator;

	public SpanSplitEnumerator(ReadOnlySpan<char> source, ReadOnlySpan<char> separator)
	{
		_string = source;
		_separator = separator;
	}

	// ReadOnlySpan<char> 타입을 Element로 제공
	public ReadOnlySpan<char> Current { get; private set; }

	// 자기 자신을 열거자로 제공
	public SpanSplitEnumerator GetEnumerator() => this;

	public bool MoveNext()
	{
		// 열거가 가능한지 확인
	}
}
```

foreach (var item in SapnSplitEnumerator) 같은 방법으로 열거자를 순회한다고 치면, GetEnumerator 메서드를 통해 SpanSplitEnumerator를 열거자로 제공받고, 그 내부의 Current와 MoveNext를 호출하는 식으로 반복문이 이루어질 것이다.

그럼 이제 해야 할 것은 단순히 MoveNext를 통해 계속 열거가 가능한지 (즉 문자열의 끝에 도달하지 않았는지)를 제공하고, 그 때마다 Split된 부분에 해당하는 문자열의 뷰를 Current에 적절히 할당해 주기만 하면 된다.

앞서 말했듯이 Span 및 ReadOnlySpan에는 IndexOf 메서드가 구현되어 있기 때문에, 문자열 내에 구분자가 있는지 확인한 후 있다면 그 인덱스까지 잘라 Current에 넣어주고, 없다면 (즉 -1을 반환한다면) 남은 문자열을 싸그리 Current에 넣어준 후 다음에 호출될 때 false를 반환해 주도록 하면 간단하다.

```csharp
public bool MoveNext()
{
	if (_isEnd)
	{
		return false;
	}

	int index = _string.IndexOf(_separator);
	if (index >= 0)
	{
		Current = _string[..index];
		_string = _string[(index + _separator.Length)..];
	}
	else
	{
		Current = _string;
		_string = ReadOnlySpan<char>.Empty;
		_isEnd = true;
	}

	return true;
}
```

이제 열거자를 구현했으니 Split 메서드를 구현할 차례인데, 구분자가 한 개인 경우와 연속된 문자열인 경우 두 가지를 모두 고려해 만들어 보자. 간단하게, 원본 문자열과 구분자를 ReadOnlySpan 타입으로 받아 SpanSplitEnumerator 구조체를 생성한 후 리턴하기만 하면 된다.

```csharp
public static SpanSplitEnumerator Split(this ReadOnlySpan<char> src, char separator) => new(src, new char[] { separator });

public static SpanSplitEnumerator Split(this ReadOnlySpan<char> src, ReadOnlySpan<char> separator) => new(src, separator);
```

이제 foreach문을 돌려보면, 다음과 같은 결과를 얻을 수 있다.

```csharp
string str = "Hello, World!";
var span = str.AsSpan();

Console.WriteLine("========== string.Split ==========");
foreach (var split in str.Split(',').Select((s, index) => (Chars: s, Index: index)))
{
	Console.WriteLine($"{split.Chars}");
}

Console.WriteLine();
Console.WriteLine("========== ReadOnlySpan<char>.Split ==========");
foreach (var split in span.Split(','))
{
	Console.WriteLine($"{split}");
}

/*
Output
========== string.Split ==========
Hello
 World!

========== ReadOnlySpan<char>.Split ==========
Hello
 World!
*/
```

보다시피, string 타입의 Split 메서드와 동일한 기능을 하는 것을 볼 수 있다.

그럼 구현 끝!

...이어도 좋겠지만... 이대로만 끝내기에는 뭔가 아쉬운 점이 있다.

문자열을 Split 할때는 Split된 문자열 자체 뿐만 아니라, 그 문자열이 구분자를 기준으로 원본 문자열의 몇 번째에 있었느냐 하는 것도 필요할 수가 있다.

가령 csv를 파싱하는데 첫 번째 셀은 int로 파싱하고, 두 번째 셀은 json 객체로 deserialize하고, 세 번째 셀은 그냥 문자열로 처리해야 한다든지 하는 경우에는 Split 후 Index를 함께 가져오는 것이 필요할 수 있다.

이 역시 구현은 어렵지 않은데, Split된 문자열(ReadOnlySpan 타입)과 Index를 멤버로 갖는 구조체를 정의해 주고, MoveNext에서 이 구조체의 인스턴스를 생성해 Current에 할당해 주기만 하면 된다.

먼저 Split된 문자열과 Index를 노출하는 객체를 SpanSplitEnumerator 구조체 내부에 동일하게 ref struct 타입으로 정의한다.

```csharp
public readonly ref struct SpanSplitEntry
{
	internal SpanSplitEntry(ReadOnlySpan<char> chars, int index)
	{
		Chars = chars;
		Index = index;
	}

	public ReadOnlySpan<char> Chars { get; }

	public int Index { get; }
}
```

Split된 문자열과 인덱스를 가져오는 게 주 목적이고 값을 수정할 일은 없으므로, 이를 명확히 하기 위해 readonly 타입 구조체로 정의해 주었다.

그 다음으로는 앞서 만든 SpanSplitEnumerator 구조체의 Current 프로퍼티의 타입을 새로 만들어준 SpanSplitEntry로 변경해 주고, MoveNext를 수정해 주자.

```csharp
public ref struct SpanSplitEnumerator
{
	// 나머지는 다 동일!
	private int _index;

	public SpanSplitEnumerator(ReadOnlySpan<char> source, ReadOnlySpan<char> separator)
	{
		//...
		_index = 0;
	}

	public SpanSplitEntry Current { get; private set; }

	public bool MoveNext()
	{
		if (_isEnd)
		{
			return false;
		}

		int index = _string.IndexOf(_separator);
		if (index >= 0)
		{
			Current = new SpanSplitEntry(_string[..index], _index++);
			_string = _string[(index + _separator.Length)..];
		}
		else
		{
			Current = new SpanSplitEntry(_string, _index++);
			_string = ReadOnlySpan<char>.Empty;
			_isEnd = true;
		}

		return true;
	}
}
```

크게 바뀐 부분은 없다.

Current를 변경하는 부분을 ReadOnlySpan\<char\> 타입의 뷰를 할당하는 것에서 SpanSplitEntry를 생성해 할당하는 것으로 바꾸었고, 시퀀스마다 index의 값을 하나씩 증가해 몇 번째 인덱스인지 알 수 있도록 했다.

이제 테스트 코드를 살짝 수정해 돌려보면, 정상적으로 잘 동작하는 것을 확인할 수 있다.

```csharp
string str = "Hello, World!";
var span = str.AsSpan();

Console.WriteLine("========== string.Split ==========");
foreach (var split in str.Split(',').Select((s, index) => (Chars: s, Index: index)))
{
	Console.WriteLine($"index {split.Index}: {split.Chars}");
}

Console.WriteLine();
Console.WriteLine("========== ReadOnlySpan<char>.Split ==========");
foreach (var split in span.Split(','))
{
	Console.WriteLine($"index {split.Index}: {split.Chars}");
}

/*
Output
========== string.Split ==========
index 0: Hello
index 1:  World!

========== ReadOnlySpan<char>.Split ==========
index 0: Hello
index 1:  World!
*/
```

보다시피 정상적으로 동작한다.

그 외에 구분자가 여러 개인 경우에도, IndexOf 대신 IndexOfAny를 사용하는 식으로 조금만 바꿔주면 된다. char 타입이 아닌 byte 타입인 경우 등에는 제네릭으로 구현하면 되지 않을까 생각한다. (아마도...)

그럼 ReadOnlySpan\<char\> 타입의 Split 구현 진짜 끝!
