using System;
using System.Linq;
using System.Threading.Tasks;
using Marten;
using Marten.Services;
using Marten.Testing.Documents;
using Marten.Testing.Harness;
using Shouldly;
using Weasel.Postgresql;
using Xunit;

namespace DocumentDbTests.Writing;

public class storing_documents : IntegrationContext
{
    public storing_documents(DefaultStoreFixture fixture) : base(fixture)
    {
    }



    [Theory]
    [InlineData(Marten.DocumentTracking.IdentityOnly)]
    [InlineData(Marten.DocumentTracking.None)]
    public void store_a_document(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        theSession.Store(user);
        theSession.SaveChanges();

        using (var session3 = theStore.OpenSession())
        {
            var user3 = session3.Load<User>(user.Id);
            user3.FirstName.ShouldBe("James");
            user3.LastName.ShouldBe("Worthy");
        }
    }

    [Theory]
    [InlineData(Marten.DocumentTracking.IdentityOnly)]
    [InlineData(Marten.DocumentTracking.None)]
    public void store_and_update_a_document_then_document_should_not_be_updated(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        theSession.Store(user);
        theSession.SaveChanges();

        using (var session2 = theStore.OpenSession())
        {
            session2.ShouldNotBeSameAs(theSession);

            var user2 = session2.Load<User>(user.Id);
            user2.FirstName = "Jens";
            user2.LastName = "Pettersson";

            session2.SaveChanges();
        }

        using (var session3 = theStore.OpenSession())
        {
            var user3 = session3.Load<User>(user.Id);
            user3.FirstName.ShouldBe("James");
            user3.LastName.ShouldBe("Worthy");
        }
    }

    [Theory]
    [InlineData(Marten.DocumentTracking.IdentityOnly)]
    [InlineData(Marten.DocumentTracking.None)]
    public void store_and_update_a_document_in_same_session_then_document_should_not_be_updated(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        theSession.Store(user);
        theSession.SaveChanges();

        user.FirstName = "Jens";
        user.LastName = "Pettersson";
        theSession.SaveChanges();

        using (var session3 = theStore.OpenSession())
        {
            var user3 = session3.Load<User>(user.Id);
            user3.FirstName.ShouldBe("James");
            user3.LastName.ShouldBe("Worthy");
        }
    }

    [Theory]
    [InlineData(Marten.DocumentTracking.IdentityOnly)]
    [InlineData(Marten.DocumentTracking.None)]
    public void store_reload_and_update_a_document_in_same_session_then_document_should_not_be_updated(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        theSession.Store(user);
        theSession.SaveChanges();

        var user2 = theSession.Load<User>(user.Id);
        user2.FirstName = "Jens";
        user2.LastName = "Pettersson";
        theSession.SaveChanges();

        using (var session = theStore.OpenSession())
        {
            var user3 = session.Load<User>(user.Id);
            user3.FirstName.ShouldBe("James");
            user3.LastName.ShouldBe("Worthy");
        }
    }

    [Fact]
    public void store_document_inherited_from_document_with_id_from_another_assembly()
    {
        using (var session = theStore.OpenSession())
        {
            var user = new UserFromBaseDocument();
            session.Store(user);
            session.Load<UserFromBaseDocument>(user.Id).ShouldBeTheSameAs(user);
        }
    }

    [Theory]
    [SessionTypes]
    public void persist_a_single_document(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "Magic", LastName = "Johnson" };


        theSession.Store(user);

        theSession.SaveChanges();

        using var conn = theStore.Tenancy.Default.Database.CreateConnection();
        conn.Open();

        var reader = conn.CreateCommand($"select data from mt_doc_user where id = '{user.Id}'").ExecuteReader();
        reader.Read();

        var loadedUser = new JsonNetSerializer().FromJson<User>(reader, 0);

        user.ShouldNotBeSameAs(loadedUser);
        loadedUser.FirstName.ShouldBe(user.FirstName);
        loadedUser.LastName.ShouldBe(user.LastName);
    }

    [Theory]
    [SessionTypes]
    public void persist_and_reload_a_document(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        // theSession is Marten's IDocumentSession service
        theSession.Store(user);
        theSession.SaveChanges();

        using (var session2 = theStore.OpenSession())
        {
            session2.ShouldNotBeSameAs(theSession);

            var user2 = session2.Load<User>(user.Id);

            user.ShouldNotBeSameAs(user2);
            user2.FirstName.ShouldBe(user.FirstName);
            user2.LastName.ShouldBe(user.LastName);
        }
    }

    [Theory]
    [SessionTypes]
    public async Task persist_and_reload_a_document_async(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user = new User { FirstName = "James", LastName = "Worthy" };

        // theSession is Marten's IDocumentSession service
        theSession.Store(user);
        await theSession.SaveChangesAsync();

        using (var session2 = theStore.OpenSession())
        {
            session2.ShouldNotBeSameAs(theSession);

            var user2 = await session2.LoadAsync<User>(user.Id);

            user.ShouldNotBeSameAs(user2);
            user2.FirstName.ShouldBe(user.FirstName);
            user2.LastName.ShouldBe(user.LastName);
        }
    }

    [Theory]
    [SessionTypes]
    public void try_to_load_a_document_that_does_not_exist(DocumentTracking tracking)
    {
        DocumentTracking = tracking;
        theSession.Load<User>(Guid.NewGuid()).ShouldBeNull();
    }

    [Theory]
    [SessionTypes]
    public void load_by_id_array(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        var user1 = new User { FirstName = "Magic", LastName = "Johnson" };
        var user2 = new User { FirstName = "James", LastName = "Worthy" };
        var user3 = new User { FirstName = "Michael", LastName = "Cooper" };
        var user4 = new User { FirstName = "Mychal", LastName = "Thompson" };
        var user5 = new User { FirstName = "Kurt", LastName = "Rambis" };

        theSession.Store(user1);
        theSession.Store(user2);
        theSession.Store(user3);
        theSession.Store(user4);
        theSession.Store(user5);
        theSession.SaveChanges();

        using (var session = theStore.OpenSession())
        {
            var users = session.LoadMany<User>(user2.Id, user3.Id, user4.Id);
            users.Count().ShouldBe(3);
        }
    }

    [Theory]
    [SessionTypes]
    public async Task load_by_id_array_async(DocumentTracking tracking)
    {
        DocumentTracking = tracking;

        #region sample_saving-changes-async

        var user1 = new User { FirstName = "Magic", LastName = "Johnson" };
        var user2 = new User { FirstName = "James", LastName = "Worthy" };
        var user3 = new User { FirstName = "Michael", LastName = "Cooper" };
        var user4 = new User { FirstName = "Mychal", LastName = "Thompson" };
        var user5 = new User { FirstName = "Kurt", LastName = "Rambis" };

        theSession.Store(user1);
        theSession.Store(user2);
        theSession.Store(user3);
        theSession.Store(user4);
        theSession.Store(user5);

        await theSession.SaveChangesAsync();

        #endregion

        var store = theStore;

        #region sample_load_by_id_array_async

        using (var session = store.OpenSession())
        {
            var users = await session.LoadManyAsync<User>(user2.Id, user3.Id, user4.Id);
            users.Count().ShouldBe(3);
        }

        #endregion
    }
}