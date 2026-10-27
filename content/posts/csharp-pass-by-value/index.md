---
title: "[C#/기본기] 참조 타입을 매개변수에 전달할 때는 Pass by reference일까?"
slug: csharp-pass-by-value
date: 2023-07-13
summary: "C#에서 참조 형식을 매개변수로 넘기면 Pass by reference일까? 결론부터 말하면 아니다."
tags: [csharp]
draft: false
---

c#을 사용하다 보면, 메서드에 참조 형식(class, interface, delegate 등...)을 매개변수에 전달해야 할 일이 빈번하게 발생한다.

## 값 형식: Pass by value

int, byte, 구조체 등의 값 형식 변수를 매개변수에 전달할 때는 Pass by value로 동작하는 것은 웬만하면 모두 인지하고 있을 것이다. 즉 메서드에 변수를 전달할 때 값 복사가 일어나게 되며, 메서드 내에서 변수의 값을 수정하더라도 원본에는 반영되지 않는다.

```csharp
internal class Program
{
	static void Main(string[] args)
	{
		int x = 5;
		Add(x, 3);
		System.Console.WriteLine($"x={x}");
	}

	static void Add(int x, int y)
	{
		x = x + y;
	}
}
// output: x=5
```

짐작하다시피, 위 코드의 출력값은 x=5이다.

Add 메서드 내에서 이루어진 값 변경이 원본 변수에 반영되게 하려면 ref 등의 키워드로 참조에 의한 전달(Pass by reference)로 동작하게 해야 한다.

```csharp
internal class Program
{
	static void Main(string[] args)
	{
		int x = 5;
		Add(ref x, 3);
		System.Console.WriteLine($"x={x}");
	}

	static void Add(ref int x, int y)
	{
		x = x + y;
	}
}
// output: x=8
```

이제는 기대한 대로 x에 저장된 값이 수정되고, x=8이 출력된다.

그 외에도 값 형식을 매개변수로 넘기면 메서드가 호출될 때 값 복사가 일어나기 때문에, 변수의 크기가 큰 경우 오버헤드가 발생할 수 있다. 이런 경우에도 Pass by reference로 동작하게 하는 것을 고려해 볼 수 있다.

## 본론으로 돌아와서...

그러면 매개변수 형식이 값 형식이 아닌 클래스 같은 참조 형식이라고 생각해 보자.

```csharp
internal class Program
{
	static void Main(string[] args)
	{
		Person person = new Person();
		person.Name = "철수";
		ChangeName(person, "영희");
		System.Console.WriteLine(person.Name);
	}

	static void ChangeName(Person person, string name)
	{
		person.Name = name;
	}
}

public class Person
{
	public string Name { get; set; }
}
// output: 영희
```

위 코드의 출력값은 영희다. 참조 형식인 Person 클래스를 매개변수로 넣었으므로, 객체의 프로퍼티인 Name을 ChangeName 메서드에서 수정하면 원본 객체에도 반영되기 때문이다.

그렇다면 참조 형식은 Pass by reference(Call by reference)인가?

일부러 앞에서 언급하지 않고 넘어갔는데, c#에는 class 외에도 굉장히 자주 쓰는 참조 형식이 또 있다.

바로 string이다.

c#에서 string은 불변 '객체' 어쩌고... gc 압력을 최소화하기 위해 StringBuilder가 저쩌고... 하는 걸 자주 봤을 것이다. 당연히 string이 참조 형식이고, 힙에 올라가기 때문에 이런 말들이 나오는 것이다.

어쨌든 c#에서 참조 형식이 Pass by reference로 동작한다면, 참조 형식인 string 역시 메서드 안에서 원본 객체에 접근할 수 있어야 한다.

```csharp
internal class Program
{
	static void Main(string[] args)
	{
		string message = "Hello";
		Add(message, ", World!");
		System.Console.WriteLine(message);
	}

	static void Add(string str1, string str2)
	{
		str1 = str1 + str2;
	}
}
// output: Hello
```

코드를 보고 나서 Hello, World가 출력되기를 기대하지는 않았기를...

위 코드의 출력값은 당연히 Hello다.

왜 그럴까? 당연히 결론은 간단명료하다.

## 참조 형식도 Pass by value다

일반적으로 참조 형식에 대한 변경이라고 해봐야 대부분 클래스에 접근해 멤버 변수를 조작하는 선에서 그치고, 새로 객체를 할당하는 경우가 아닌 이상 이런 문제를 마주할 일이 적다 보니 간과하기 쉽지만, 값 형식이든 참조 형식이든 기본적으로는 Pass by value, 정확히는 Pass a reference type by value로 동작한다. 즉, 전달 과정에서 값이 복사된다. 다만 값 형식은 변수에 저장된 값이 복사되고, 참조 형식은 변수가 참조하고 있는 주소가 복사되는 점이 다를 뿐이다. 아니, 참조 형식 변수에 저장된 값이 결국 참조하고 있는 객체의 주소값이므로, 사실 어떻게 보면 둘다 동일한 동작을 하는 셈으로 볼 수도 있다.

- 값 형식 - 변수에 저장된 값의 복사본을 전달
- 참조 형식 - 변수가 참조하는 메모리 주소의 복사본을 전달

이제, 위에서 Person 클래스를 조작하는 코드를 조금만 수정해서 다시 살펴보자.

```csharp
internal class Program
{
	static void Main(string[] args)
	{
		Person person = new Person();
		person.Name = "철수";
		ChangeName(person, "영희");
		System.Console.WriteLine(person.Name);
	}

	static void ChangeName(Person person, string name)
	{
		person = new Person();
		person.Name = name;
	}
}

public class Person
{
	public string Name { get; set; }
}
// output: 철수
```

달라진 부분은 ChangeName 메서드에서 person 변수에 새 객체를 할당해준 것밖에 없다. 그러나 이번에는, 영희가 아니라 철수가 출력된다.

Main 메서드에서의 person 변수와, ChangeName 메서드에서의 person 변수는 이름만 같을 뿐, 같은 메모리 주소를 참조하는 다른 변수다. 그러므로 ChangeName에서 person에 새 객체를 할당해 변수가 참조하는 주소값을 수정하는 순간 서로 다른 주소를 참조하게 되고, 이후 멤버 변수를 수정하든 말든 서로 무관해지게 된다. 다른 객체니까.

**즉 클래스 등의 참조 형식도, Pass by reference(Call by reference)로 동작하게 하려면 ref 등의 키워드를 붙여야 한다.**

어떻게 보면 간단한 건데, 은근히 헷갈리기 쉬운 부분이다. 참조 형식, 참조에 의한 전달. 둘다 참조가 들어가 있어서 그런 건지...

단순히 헷갈리는 걸 넘어 c#에서 클래스가 Call by reference로 동작한다고 당연하다는 듯 설명하는 블로그도 종종 보이기에 안타까워 다뤄본다.

**c#에서도 매개변수에 인수를 전달할 땐, 기본적으로 Pass by value다.**
