#ifndef _LAYEC_LIBC_STDDEF_H_
#define _LAYEC_LIBC_STDDEF_H_

#define _LAYEC_LIBC_STDDEF_DO_THE_THING

#if defined(__has_include_next)
#  if __has_include_next(<stddef.h>)
#    undef _LAYEC_LIBC_STDDEF_DO_THE_THING
#    include_next <stddef.h>
#  endif
#endif

#ifdef _LAYEC_LIBC_STDDEF_DO_THE_THING

#ifdef __SIZE_TYPE__
typedef __SIZE_TYPE__ size_t;
#else
#  error __SIZE_TYPE__ not defined.
#endif

#ifdef __PTRDIFF_TYPE__
typedef __PTRDIFF_TYPE__ ptrdiff_t;
#else
#  error __PTRDIFF_TYPE__ not defined.
#endif

#ifdef __WCHAR_TYPE__
typedef __WCHAR_TYPE__ wchar_t;
#else
#  error __WCHAR_TYPE__ not defined.
#endif

#endif

#endif /* _LAYEC_LIBC_STDDEF_H_ */
