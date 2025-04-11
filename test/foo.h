#define X0(A, B) A + B
#define X1(A, ...) A + __VA_ARGS__

#define CAT_(A, B) A ## B
#define CAT(A, B) CAT_(A, B)

#define FOO 10
#define BAR FOO

int x = X0(FOO, 20);
int x = X1(FOO, 20 * 2);

int CAT_(BAZ, BAR);
int CAT(BAZ, BAR);
