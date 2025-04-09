typedef struct Color {
    int r, g, b;
} Color;
void SetBackgroundColorImpl_(Color color);

#define CLITERAL(R, G, B) (Color){(R), (G), (B)}
#define SetBackgroundColor(C) SetBackgroundColorImpl_(C)
