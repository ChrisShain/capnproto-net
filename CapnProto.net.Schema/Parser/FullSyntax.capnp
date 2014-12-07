
# Todo: to move somewhere else

# Intention: this file contains everything the syntax allows and passes capnp itself.

annotation baz(*) :Foobar;
annotation voidAn(*): Void;

const constantFoo :Foobar = ( blah = "constant" );

struct Foobar $voidAn {
  blah @0: Text = "foobar";
}

struct AnnotationTest $baz(.constantFoo) {}

# Can declare a struct called List.
# It's apparently possible to override List(T), but should check. TODO
# struct List(T) {} 

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

enum NonOrderedEnum {
  foo @1;
  bar @0;
}

# Todo: stuff that resolves as "Text".
struct TextTest {
  struct Text {}
}

struct GenericStruct(T, U) {
   # This works: TODO
   # struct Text {}
}
struct GenericTest1 {
   foo @0: GenericStruct(Text, Text);
}


#struct GenericTest(T) {
#  foo @0: T; # < which T is this? todo
#  struct T {}
#}
#
#struct GenericTest2 {
#  gen @0: GenericTest(GenericTest.T);
##  
#  # This does not appear to work, capnp parser throws an error on "test".
#  # x @1: GenericTest(Text) = (foo = "test");
#}
#
#interface IGenericInterface(TFoobar) {
#   blah @0 (x: TFoobar) -> (y: TFoobar);
#}
#
## A partially constructed generic type.
#interface ISimple{}
#interface ITwoGenericParams(T,U) { }
#interface IInheritFromTwoGen(T) extends(ITwoGenericParams(T, Text)) {
#   call @0 (x: T);
#}
#
#interface IGeneric(T, U) {
#   annotation foobar(*):T;
#}

interface IFoo2
{
   annotation foo(*): Text;
 }
interface IBar2 extends(IFoo2) {

}

# Hah, so much for inheritance. The following does not work in capnp:
#struct Foo2Test $IBar2.foo("text") {

#}

struct A(T) {
  # Note: there appears no way to refer to the outer T within B. This compiles OK though.
  struct B(T) {
     x @0: T; 
  }
}

struct Test(T) {
   struct Test2(U) {
      x @0: T;
      
      annotation test2(*): U;
   }
}

## Wont work with U= Int32 here -> only pointer types as generic params. TODO detect this too
#struct TestTest $Test(Text).Test2(List(Int32)).test2([2]) {
#
#}
#struct TestTest2(T) $Test(T).Test2(Text).test2("foo") {
#}

interface INoBrackets {
   # Instead of using brackets, it appears you can use a struct as parameters.
   # TODO
   # test @0 UseNameBeforeUsing -> (qux: Text);
   
   # TODO: implicit parameters
}

struct UseNameBeforeUsing {
   ref @0: TRef;
   using TRef = Foobar;
   
   # This does not work.
   # enumVal @1: NonOrderedEnum = NonOrderedEnum.foo;
   # This, however, does.
   enumVal @1: NonOrderedEnum = foo;
}

struct NameUniqueness {
   #const Foobar : Text = "blah";
   #struct Foobar {}
}

struct NestingStruct {
   enum Foobar { }
   struct Nested {}
   interface INested {}
   const nestedKonst: Text = "foo";
   annotation nestedAnnot(*): Text;
}
interface INestingInterface {
   enum Foobar { }
   struct Nested {}
   interface INested {}
   const nestedKonst: Text = "foo";
   annotation nestedAnnot(*): Text;
}

@0x9eb32e19f86ee174;

