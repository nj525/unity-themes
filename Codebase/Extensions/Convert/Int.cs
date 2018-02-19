using System;
namespace Zios.Extensions.Convert{
	public static class ConvertInt{
		public static string ToHex(this int current){return current.ToString("X6");}
		public static Enum ToEnum(this int current,Type enumType){return (Enum)Enum.ToObject(enumType,current);}
		public static T ToEnum<T>(this int current){return (T)Enum.ToObject(typeof(T),current);}
		public static bool ToBool(this int current){return current != 0;}
		public static byte ToByte(this int current){return (byte)current;}
		public static short ToShort(this int current){return (short)current;}
		public static byte[] ToBytes(this int current){return BitConverter.GetBytes(current);}
		public static string Serialize(this int current){return current.ToString();}
		public static int Deserialize(this int current,string value){return value.ToInt();}
	}
}