// GroupedString.h: interface for the CGroupedString class.
//
//////////////////////////////////////////////////////////////////////

#if !defined(AFX_GROUPEDSTRING_H__3207E646_9419_4CC7_A65A_A14BF2D03749__INCLUDED_)
#define AFX_GROUPEDSTRING_H__3207E646_9419_4CC7_A65A_A14BF2D03749__INCLUDED_

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

class CGroupedString : public CStringArray
{
public:
	CString Get(BOOL& bTruncated, BOOL bSpaceAlignment = TRUE, LPCSTR lpGroupNameSeparator = "  \t: ");
	void Add(LPCSTR lpValue, LPCSTR lpGroup, LPCSTR lpSeparator, LPCSTR lpGroupDataSeparator = ", ");
	CGroupedString(int nMaxLen);
	virtual ~CGroupedString();
protected:
	int m_nMaxLen;
};

#endif // !defined(AFX_GROUPEDSTRING_H__3207E646_9419_4CC7_A65A_A14BF2D03749__INCLUDED_)
