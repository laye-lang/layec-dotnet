// this defines foo
#define FOO(...) __VA_OPT__(,)
int foo();
