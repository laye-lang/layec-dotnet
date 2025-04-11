// Macro Expansion Tests
//
// NOTE: Throughout this file, empty expansions should be on a
// line of their own. Our pretty printer isn’t clever enough to
// e.g. realise that 'b' in 'a b' should be printed on a separate
// line if 'a' expands to nothing.
//

#define A
A
A A
A A A A A A
#undef A

// + 123
// + 123 123 123 123
#define A 123
A
A A A A
#undef A

// + A A
A A

// + A
// + A)
// + +
#define A(a) a + a
A
A)
A()

// + 1 + 1
// + 2 + 2 3 + 3
A(1)
A(2) A(3)
#undef A

// + a b ab 1234 ++ -= == == ==
#define A(x, y) x##y
A(,)
A(a,) A(,b) A(a,b) A(12,34) A(+, +) A(-,=) A(==,) A(,==) A(  =  ,    =)
#undef A
