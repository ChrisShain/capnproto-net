
# Todo: to move somewhere else

# Intention: this file contains everything the syntax allows and passes capnp itself.

annotation baz(*) :Foobar;
annotation voidAn(*): Void;

const constantFoo :Foobar = ( blah = "constant" );

struct Foobar $voidAn {
  blah @0: Text = "foobar";
}

struct AnnotationTest $baz(.constantFoo) {}

struct DefaultValueTests $baz((((blah = "baz")))) # < notice that many brackets are allowed by capnp tool
{
   text @0: Text = "foobar: \t\0 \12 \123 \xAB ";
   blob @1: Data = 0x" AB 12 ABCD  ";
   
   hex @2: Int32 = 0x123;
   negInt @03: Int32 = - 1;
   octalVal @04: Int32 = 0123;
   octalZero @5: Int32 = 0;
   
   infinity @6: Float32 = inf;
   negInf @7: Float64 = - inf ;
   
   test @8: Float32 = (((0.23)));
   
   foobar @9: Foobar = (((((( blah = "foobar" ))))));
}

struct UseNameBeforeUsing {
   ref @0: TRef;
   using TRef = Foobar;
}

@0x9eb32e19f86ee174;

