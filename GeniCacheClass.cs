using FamilyTreeLibrary.FamilyData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;

namespace FamilyTreeCodecGeni
{
  [DataContract]
  public class GeniCache
  {
    private static readonly TraceSource trace = new TraceSource("GeniCache", SourceLevels.Warning);
    [DataMember]
    private IDictionary<string, IndividualClass> individuals;
    [DataMember]
    private IDictionary<string, FamilyClass> families;
    [DataMember]
    private IDictionary<string, List<string>> parentsI2fReference;
    [DataMember]
    private IDictionary<string, List<string>> childrenI2fReference;
    [DataMember]
    private IDictionary<string, List<string>> parentsF2iReference;
    [DataMember]
    private IDictionary<string, List<string>> childrenF2iReference;
    [DataMember]
    private DateTime latestUpdate;
    private const int CACHE_CLEAR_DELAY = 3600 * 24 * 7; // one week

    private TimeSpan maxCacheTime = TimeSpan.FromDays(1);

    class AddFamilyEvent : EventArgs
    {
      public FamilyClass family;
      public AddFamilyEvent(FamilyClass family)
      {
        this.family = family;
      }
    }
    class AddIndividualEvent : EventArgs
    {
      public IndividualClass individual;
      public AddIndividualEvent(IndividualClass individual)
      {
        this.individual = individual;
      }
    }

    public GeniCache()
    {
      individuals = new Dictionary<string, IndividualClass>();
      families = new Dictionary<string, FamilyClass>();
      latestUpdate = DateTime.Now;
      parentsF2iReference = new Dictionary<string, List<string>>();
      childrenF2iReference = new Dictionary<string, List<string>>();
      parentsI2fReference = new Dictionary<string, List<string>>();
      childrenI2fReference = new Dictionary<string, List<string>>();
    }

    void UpdateF2iReferences(FamilyClass family)
    {
      int checkNo = 0;
      int addedNo = 0;
      IList<IndividualXrefClass> children = family.GetChildList();
      IList<IndividualXrefClass> parents = family.GetParentList();
      lock (childrenF2iReference)
      {
        foreach (IndividualXrefClass individual in children)
        {
          if (!childrenF2iReference.ContainsKey(individual.GetXrefName()))
          {
            childrenF2iReference.Add(individual.GetXrefName(), new List<string>());
          }
          if (!childrenF2iReference[individual.GetXrefName()].Contains(family.GetXrefName()))
          {
            childrenF2iReference[individual.GetXrefName()].Add(family.GetXrefName());
            addedNo++;
          }
          checkNo++;
        }
      }
      lock (parentsF2iReference)
      {
        foreach (IndividualXrefClass individual in parents)
        {
          if (!parentsF2iReference.ContainsKey(individual.GetXrefName()))
          {
            parentsF2iReference.Add(individual.GetXrefName(), new List<string>());
          }
          if (!parentsF2iReference[individual.GetXrefName()].Contains(family.GetXrefName()))
          {
            parentsF2iReference[individual.GetXrefName()].Add(family.GetXrefName());
            addedNo++;
          }
          checkNo++;
        }
      }
    }

    void UpdateI2fReferences(IndividualClass individual)
    {
      int checkNo = 0;
      int addedNo = 0;
      lock (childrenI2fReference)
      {
        IList<FamilyXrefClass> childFamilies = individual.GetFamilyChildList();
        foreach (FamilyXrefClass family in childFamilies)
        {
          if (!childrenI2fReference.ContainsKey(family.GetXrefName()))
          {
            childrenI2fReference.Add(family.GetXrefName(), new List<string>());
          }
          if (!childrenI2fReference[family.GetXrefName()].Contains(individual.GetXrefName()))
          {
            childrenI2fReference[family.GetXrefName()].Add(individual.GetXrefName());
            addedNo++;
          }
          checkNo++;
        }
      }
      lock (parentsI2fReference)
      {
        IList<FamilyXrefClass> spouseFamilies = individual.GetFamilySpouseList();
        foreach (FamilyXrefClass family in spouseFamilies)
        {
          if (!parentsI2fReference.ContainsKey(family.GetXrefName()))
          {
            parentsI2fReference.Add(family.GetXrefName(), new List<string>());
          }
          if (!parentsI2fReference[family.GetXrefName()].Contains(individual.GetXrefName()))
          {
            parentsI2fReference[family.GetXrefName()].Add(individual.GetXrefName());
          }
          checkNo++;
        }
        addedNo++;
      }
    }

    void CheckI2fReferences(ref FamilyClass family)
    {
      if (parentsI2fReference.ContainsKey(family.GetXrefName()))
      {
        IList<string> parents = parentsI2fReference[family.GetXrefName()];
        if (parents.Count > family.GetParentList().Count)
        {
          trace.TraceData(TraceEventType.Warning, 0, family.GetXrefName() + " missing parents in family " + parents.Count + " > " + family.GetParentList().Count);
          foreach (string parent in parents)
          {
            family.AddRelation(new IndividualXrefClass(parent), FamilyClass.RelationType.Parent);
          }
        }
      }
      if (childrenI2fReference.ContainsKey(family.GetXrefName()))
      {
        IList<string> children = childrenI2fReference[family.GetXrefName()];
        if (children.Count > family.GetChildList().Count)
        {
          trace.TraceData(TraceEventType.Warning, 0, family.GetXrefName() + " missing children in family " + children.Count + " > " + family.GetParentList().Count);
          foreach (string child in children)
          {
            family.AddRelation(new IndividualXrefClass(child), FamilyClass.RelationType.Child);
          }
        }
      }
    }

    void CheckF2iReferences(ref IndividualClass individual)
    {
      if (parentsF2iReference.ContainsKey(individual.GetXrefName()))
      {
        IList<string> spouses = parentsF2iReference[individual.GetXrefName()];
        if (spouses.Count > individual.GetFamilySpouseList().Count)
        {
          trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " missing spouse to individual " + spouses.Count + " > " + individual.GetFamilySpouseList().Count);
          foreach (string parent in spouses)
          {
            individual.AddRelation(new FamilyXrefClass(parent), IndividualClass.RelationType.Spouse);
            trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " adding spouse-family to individual " + parent + " to " + individual.GetName());
          }
        }
      }
      if (childrenI2fReference.ContainsKey(individual.GetXrefName()))
      {
        IList<string> children = childrenI2fReference[individual.GetXrefName()];
        if (children.Count > individual.GetFamilyChildList().Count)
        {
          trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " missing child-family in individual " + children.Count + " > " + individual.GetFamilyChildList().Count);
          foreach (string child in children)
          {
            individual.AddRelation(new FamilyXrefClass(child), IndividualClass.RelationType.Child);
            trace.TraceData(TraceEventType.Verbose, 0, individual.GetXrefName() + " adding child-family to individual " + child + " to " + individual.GetName());
          }
        }
      }
    }

    void RemoveI2fReferences(string familyXref)
    {
      if (parentsI2fReference.ContainsKey(familyXref))
      {
        parentsI2fReference.Remove(familyXref);
      }
      if (childrenI2fReference.ContainsKey(familyXref))
      {
        childrenI2fReference.Remove(familyXref);
      }
    }


    void RemoveF2iReferences(string individualXref)
    {
      if (parentsF2iReference.ContainsKey(individualXref))
      {
        parentsF2iReference.Remove(individualXref);
      }
      if (childrenI2fReference.ContainsKey(individualXref))
      {
        childrenI2fReference.Remove(individualXref);
      }
    }

    void CacheFamily(FamilyClass family)
    {
      trace.TraceInformation("CacheFamily " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);

      lock (families)
      {
        if (!families.ContainsKey(family.GetXrefName()))
        {
          trace.TraceInformation("cached family-2 " + family.GetXrefName() + " " + families.Count + " " + Thread.CurrentThread.ManagedThreadId);

          latestUpdate = DateTime.Now;
          families.Add(family.GetXrefName(), family);
          UpdateF2iReferences(family);
        }
        else
        {
          trace.TraceInformation("skipped family " + family.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
        }
      }

    }

    void CacheIndividual(IndividualClass individual)
    {
      trace.TraceInformation("CacheIndidvidual " + individual.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
      lock (individuals)
      {
        if (!individuals.ContainsKey(individual.GetXrefName()))
        {
          trace.TraceInformation("cached individual-2 " + individual.GetXrefName() + " " + individuals.Count + " " + Thread.CurrentThread.ManagedThreadId);

          individuals.Add(individual.GetXrefName(), individual);
          UpdateI2fReferences(individual);
          latestUpdate = DateTime.Now;
        }
        else
        {
          trace.TraceInformation("skipped individual " + individual.GetXrefName() + " " + Thread.CurrentThread.GetApartmentState() + " " + Thread.CurrentThread.ManagedThreadId);
        }
      }
    }

    private void Clear()
    {
      Print();
      individuals.Clear();
      families.Clear();
      trace.TraceInformation(" Geni Cache cleared! " + families.Count + " families and " + individuals.Count + " people");
    }

    public bool CheckIndividual(string xrefName)
    {
      if (latestUpdate.AddSeconds(CACHE_CLEAR_DELAY) < DateTime.Now)
      {
        Clear();
      }
      if (individuals.ContainsKey(xrefName))
      {
        IndividualClass individual = individuals[xrefName];

        DateTime latestUpdate = individual.GetLatestUpdate();

        if ((DateTime.Now - latestUpdate) < maxCacheTime)
        {
          return true;
        }
        individuals.Remove(xrefName);
        RemoveF2iReferences(xrefName);
      }
      return false;
    }

    public void AddIndividual(IndividualClass individual)
    {
      if (individual == null)
      {
        trace.TraceData(TraceEventType.Error, 0, "GeniCache: Trying to add individual == null");
      }
      else if (individual.GetXrefName().Length == 0)
      {
        trace.TraceEvent(TraceEventType.Error, 0, "GeniCache:AddIndividual():error: no xref!");
      }
      else
      {
        bool relations = false;
        trace.TraceInformation("cached individual " + individual.GetXrefName());

        if (individual.GetFamilyChildList() != null)
        {
          if (individual.GetFamilyChildList().Count > 0)
          {
            relations = true;
          }
        }
        if (individual.GetFamilySpouseList() != null)
        {
          if (individual.GetFamilySpouseList().Count > 0)
          {
            relations = true;
          }
        }
        if (!relations)
        {
          if (individual.GetPublic())
          {
            string url = "";
            IList<string> urls = individual.GetUrlList();
            if (urls.Count > 0)
            {
              url = urls[0];
            }
            trace.TraceData(TraceEventType.Information, 0, "Person has no relations! " + individual.GetXrefName() + " " + url + " " + individual.GetName());
          }
          CheckF2iReferences(ref individual);
        }
        CacheIndividual(individual);
        latestUpdate = DateTime.Now;
      }
    }

    public IndividualClass GetIndividual(string xrefName)
    {
      if (CheckIndividual(xrefName))
      {
        return individuals[xrefName];
      }
      return null;
    }

    public IEnumerator<IndividualClass> GetIndividualIterator()
    {
      return individuals.Values.GetEnumerator();
    }

    public bool CheckFamily(string xrefName)
    {
      if (families.ContainsKey(xrefName))
      {
        FamilyClass family = families[xrefName];

        DateTime latestUpdate = family.GetLatestUpdate();

        if ((DateTime.Now - latestUpdate) < maxCacheTime)
        {
          return true;
        }
        families.Remove(xrefName);
        RemoveI2fReferences(xrefName);
      }
      return false;
    }


    public void AddFamily(FamilyClass family)
    {
      if (family.GetXrefName().Length == 0)
      {
        trace.TraceEvent(TraceEventType.Error, 0, "error: no xref!");
      }
      else
      {
        if (!families.ContainsKey(family.GetXrefName()))
        {
          CacheFamily(family);
        }
        else
        {
          trace.TraceData(TraceEventType.Information, 0, "family " + family.GetXrefName() + " already in cache!");
        }
      }
    }

    public FamilyClass GetFamily(string xrefName)
    {
      if (latestUpdate.AddSeconds(CACHE_CLEAR_DELAY) < DateTime.Now)
      {
        Clear();
      }
      if (CheckFamily(xrefName))
      {
        return families[xrefName];
      }
      return null;
    }
    public IEnumerator<FamilyClass> GetFamilyIterator()
    {
      return families.Values.GetEnumerator();
    }
    public int GetFamilyNo()
    {
      return families.Count;
    }
    public int GetIndividualNo()
    {
      return individuals.Count;
    }
    public void Print()
    {
      trace.TraceInformation(" Geni Cache includes " + families.Count +
        " families and " + individuals.Count +
        " people. Latest update " + latestUpdate +
        " now:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

  }
}
