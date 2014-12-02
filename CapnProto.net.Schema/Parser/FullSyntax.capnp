
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

annotation pa(param): Void;
annotation pi(interface): Void;

interface IFoo $pi {
   # Note how parameters don't have a case rule, strange.
   foobar @0 (X : Int32 $pa) -> (Y: Text = (("blah")) $pa) $voidAn;
}

annotation pe(enum): Void;
annotation pen(enumerant): Void;

enum BlahEnum $pe {
  foo @0;
  bar @1 $pen;
}
enum WithId @0xa5a69bc6a92158fc {}

struct UseNameBeforeUsing {
   ref @0: TRef;
   using TRef = Foobar;
}

struct NameUniqueness {
   #const Foobar : Text = "blah";
   #struct Foobar {}
}

@0x9eb32e19f86ee174;

