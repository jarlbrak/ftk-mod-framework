using System;

static class Program
{
    sealed class Fixture
    {
        private static readonly object shared = new object();
        private readonly object local = new object();
        private object empty = null;
        internal static object Shared { get { return shared; } }
        internal object Local { get { return local; } }
        internal object Empty { get { return empty; } }
    }
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("Failed reflection check " + checks); }
    static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
    static void Main()
    {
        Fixture actor = new Fixture();
        Check(ReferenceEquals(RuntimeFieldRead.Read(typeof(Fixture), "shared"), Fixture.Shared));
        Check(ReferenceEquals(RuntimeFieldRead.Read(actor, "local"), actor.Local));
        Check(RuntimeFieldRead.Read(actor, "empty") == actor.Empty);
        Throws<ArgumentNullException>(delegate { RuntimeFieldRead.Read(null, "local"); });
        Throws<MissingFieldException>(delegate { RuntimeFieldRead.Read(actor, "missing"); });
        Throws<MissingFieldException>(delegate { RuntimeFieldRead.Read(typeof(Fixture), "local"); });
        Throws<MissingFieldException>(delegate { RuntimeFieldRead.Read(actor, "shared"); });
        Console.WriteLine(checks + " runtime field observation checks passed.");
    }
}
