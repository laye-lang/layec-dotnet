#define URMOM
//#define YOOOO
#define URDAD
#define NAHHH
#define FOURTWENTY

#ifdef URMOM
"URMOM Defined!";
#endif

#ifdef URMOM

#ifdef URDAD
    "URDAD Defined! (like that's going to happen)";
    #ifdef YOOOO
        "Is yoooo defined? yes it is";
    #elifdef NAHHH
        "NAHHHHH";
    #else
        "Is yoooo defined? it is not";
    #endif
#else
    "This should print";
#endif

#ifndef FOURTWENTY
    "oh my god, where is the 420 :O";
#else
    "here is the 420 :D"
#endif

"some stuff in here uwu";
