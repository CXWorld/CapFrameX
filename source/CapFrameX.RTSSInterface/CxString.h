// CxString.h: minimal ANSI CString/CStringArray replacement on top of the C++
// standard library. MFC is not available for C++/CLI targeting .NET (NetCore),
// so this header provides exactly the API surface the RTSS integration uses.
#pragma once

#include <string>
#include <vector>
#include <cstdarg>
#include <cstdio>
#include <cstring>

#ifdef __cplusplus_cli
#include <msclr/marshal_cppstd.h>
#endif

class CString
{
public:
	CString() {}
	CString(LPCSTR value) : m_value(value != nullptr ? value : "") {}
	CString(const std::string& value) : m_value(value) {}

#ifdef __cplusplus_cli
	CString(System::String^ value)
		: m_value(value == nullptr
			? std::string()
			: msclr::interop::marshal_as<std::string>(value))
	{
	}
#endif

	operator LPCSTR() const { return m_value.c_str(); }
	char operator[](int index) const { return m_value[index]; }

	int GetLength() const { return static_cast<int>(m_value.length()); }
	BOOL IsEmpty() const { return m_value.empty() ? TRUE : FALSE; }
	void Empty() { m_value.clear(); }

	CString& operator+=(LPCSTR value)
	{
		if (value != nullptr)
			m_value += value;
		return *this;
	}

	CString& operator+=(const CString& value)
	{
		m_value += value.m_value;
		return *this;
	}

	CString& operator+=(char value)
	{
		m_value += value;
		return *this;
	}

	int Find(char value, int startIndex = 0) const
	{
		if (startIndex < 0 || startIndex > GetLength())
			return -1;
		return static_cast<int>(m_value.find(value, static_cast<size_t>(startIndex)));
	}

	int Find(LPCSTR value, int startIndex = 0) const
	{
		if (value == nullptr || startIndex < 0 || startIndex > GetLength())
			return -1;
		return static_cast<int>(m_value.find(value, static_cast<size_t>(startIndex)));
	}

	CString Mid(int startIndex, int count) const
	{
		if (startIndex < 0 || startIndex >= GetLength() || count <= 0)
			return CString();
		return CString(m_value.substr(static_cast<size_t>(startIndex),
			static_cast<size_t>(count)));
	}

	CString Mid(int startIndex) const
	{
		if (startIndex < 0 || startIndex >= GetLength())
			return CString();
		return CString(m_value.substr(static_cast<size_t>(startIndex)));
	}

	CString Left(int count) const
	{
		if (count <= 0)
			return CString();
		if (count >= GetLength())
			return *this;
		return CString(m_value.substr(0, static_cast<size_t>(count)));
	}

	CString Right(int count) const
	{
		if (count <= 0)
			return CString();
		if (count >= GetLength())
			return *this;
		return CString(m_value.substr(m_value.length() - static_cast<size_t>(count)));
	}

	void TrimLeft()
	{
		size_t start = m_value.find_first_not_of(" \t\r\n");
		m_value = start == std::string::npos ? std::string() : m_value.substr(start);
	}

	void TrimRight()
	{
		size_t end = m_value.find_last_not_of(" \t\r\n");
		m_value = end == std::string::npos ? std::string() : m_value.substr(0, end + 1);
	}

	void Trim()
	{
		TrimRight();
		TrimLeft();
	}

	// C4793: a variadic function cannot be emitted as MSIL, so under /clr the compiler falls back
	// to native code generation for Format and reports what it did. That is the intended outcome -
	// the whole point of this helper is printf-style formatting against the CRT - and the callers
	// are native C++ anyway. Nothing to fix, so the notice is silenced where it originates.
#pragma warning(push)
#pragma warning(disable : 4793)
	void Format(LPCSTR format, ...)
	{
		if (format == nullptr)
		{
			m_value.clear();
			return;
		}

		va_list arguments;
		va_start(arguments, format);
		int length = _vscprintf(format, arguments);
		va_end(arguments);

		if (length <= 0)
		{
			m_value.clear();
			return;
		}

		std::string buffer(static_cast<size_t>(length), '\0');
		va_start(arguments, format);
		vsnprintf(&buffer[0], buffer.size() + 1, format, arguments);
		va_end(arguments);
		m_value = buffer;
	}
#pragma warning(pop)

	const std::string& Std() const { return m_value; }

private:
	std::string m_value;
};

inline CString operator+(const CString& left, const CString& right)
{
	CString result(left);
	result += right;
	return result;
}

inline CString operator+(const CString& left, LPCSTR right)
{
	CString result(left);
	result += right;
	return result;
}

inline CString operator+(LPCSTR left, const CString& right)
{
	CString result(left);
	result += right;
	return result;
}

inline bool operator==(const CString& left, const CString& right)
{
	return left.Std() == right.Std();
}

inline bool operator==(const CString& left, LPCSTR right)
{
	return right != nullptr && left.Std().compare(right) == 0;
}

inline bool operator==(LPCSTR left, const CString& right)
{
	return right == left;
}

inline bool operator!=(const CString& left, const CString& right)
{
	return !(left == right);
}

inline bool operator!=(const CString& left, LPCSTR right)
{
	return !(left == right);
}

inline bool operator!=(LPCSTR left, const CString& right)
{
	return !(right == left);
}

class CStringArray
{
public:
	virtual ~CStringArray() {}

	int GetSize() const { return static_cast<int>(m_items.size()); }
	const CString& GetAt(int index) const { return m_items[index]; }
	void SetAt(int index, const CString& value) { m_items[index] = value; }
	void Add(const CString& value) { m_items.push_back(value); }
	void RemoveAll() { m_items.clear(); }

private:
	std::vector<CString> m_items;
};
