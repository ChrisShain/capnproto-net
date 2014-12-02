
# Todo: to move somewhere else

struct DefaultValueTests
{
   text @0: Text = "foobar: \t\0 \12 \123 \xAB ";
   blob @1: Data = 0x" AB 12 ABCD  ";
   
   hex @2: Int32 = 0x123;
   negInt @03: Int32 = - 1;
   octalVal @04: Int32 = 0123;
   octalZero @5: Int32 = 0;
   
   infinity @6: Float32 = inf;
   negInf @7: Float64 = - inf ;
   
   test @8: Float32 = 0.23;
}

@0x9eb32e19f86ee174;

